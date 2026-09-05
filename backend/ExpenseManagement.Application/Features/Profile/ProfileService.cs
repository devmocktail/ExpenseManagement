using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExpenseManagement.Application.Common.Extensions;
using ExpenseManagement.Application.Common.Interfaces;
using ExpenseManagement.Application.Features.Auth;
using ExpenseManagement.Domain.Common;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Domain.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManagement.Application.Features.Profile;

/// <inheritdoc cref="IProfileService"/>
public sealed class ProfileService(
    IAppDbContext db,
    ICurrentUser currentUser,
    UserManager<ApplicationUser> userManager,
    IDateTimeProvider clock,
    IAuditService audit) : IProfileService
{
    /// <summary>
    /// The account row is not on <see cref="IAppDbContext"/> — Identity's user
    /// store owns it — so profile reads go through UserManager and come back
    /// tracked. That is the price of never hand-rolling password or security
    /// stamp handling, and it is paid on one row fetched by its primary key.
    /// </summary>
    public async Task<UserProfileDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var (user, roles) = await LoadAccountAsync(cancellationToken);
        return AuthMappings.ToProfile(user, roles);
    }

    public async Task<UserProfileDto> UpdateAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var (user, roles) = await LoadAccountAsync(cancellationToken);

        // FullName only. Email is a sign-in identity as well as a contact
        // address, so changing it needs a token sent to the new mailbox and
        // proof it came back; accepting it from a bare PUT would let whoever
        // borrows an unlocked phone move the account to their own address and
        // lock the owner out through "forgot password". Until that flow exists
        // the field is not writable at all, rather than writable and unverified.
        user.FullName = request.FullName.Trim();

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            // Identity's descriptions are already written for end users, so they
            // are passed through; the fallback covers a store that fails with no
            // description at all rather than showing the user an empty message.
            var message = string.Join(" ", result.Errors.Select(e => e.Description));

            throw new BusinessRuleException(
                string.IsNullOrWhiteSpace(message) ? "Your profile could not be updated." : message,
                "profile_update_failed");
        }

        await audit.LogAsync(
            "profile.updated",
            user.Id,
            nameof(ApplicationUser),
            user.Id,
            succeeded: true,
            cancellationToken: cancellationToken);

        return AuthMappings.ToProfile(user, roles);
    }

    public async Task<UserSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        var settings = await db.UserSettings
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(ProfileMappings.ToSettingsDto)
            .FirstOrDefaultAsync(cancellationToken);

        // The entity's own initialisers are the single source of the defaults —
        // the same ones registration writes — so a missing row and a brand new
        // account answer identically instead of disagreeing about, say, which
        // day the financial month starts on.
        return settings ?? ProfileMappings.ToSettings(new UserSettings());
    }

    public async Task<UserSettingsDto> UpdateSettingsAsync(
        UpdateSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        // Tracked, not AsNoTracking: this row is about to be written.
        var settings = await db.UserSettings
            .Where(s => s.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            settings = new UserSettings { UserId = userId };
            db.UserSettings.Add(settings);
        }

        Apply(settings, request);

        // The validator can only compare the thresholds when both are in the
        // request. A PATCH that lowers just the critical threshold below the
        // stored warning is well-formed and still violates
        // CK_UserSettings_BudgetThresholds_Ordered, so the merged pair is checked
        // here — otherwise the user gets a 500 out of the database instead of a
        // sentence telling them what is wrong.
        if (settings.BudgetCriticalThreshold < settings.BudgetWarningThreshold)
        {
            throw new BusinessRuleException(
                "Your critical alert must be at or above your warning alert, which is currently "
                + $"{settings.BudgetWarningThreshold}%.",
                "threshold_order");
        }

        await db.SaveChangesAsync(cancellationToken);

        await audit.LogAsync(
            "profile.settings_updated",
            userId,
            nameof(UserSettings),
            settings.Id,
            succeeded: true,
            cancellationToken: cancellationToken);

        return ProfileMappings.ToSettings(settings);
    }

    public async Task DeleteAccountAsync(string password, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.IsDeleted)
        {
            throw new NotFoundException("Account", userId);
        }

        // Re-authentication, not authorisation: the bearer token already proves
        // this is the account holder's session, and this proves the person
        // holding the unlocked phone is the account holder.
        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await audit.LogAsync(
                "profile.account_closed",
                userId,
                nameof(ApplicationUser),
                userId,
                succeeded: false,
                metadata: new { reason = "invalid_password" },
                cancellationToken: cancellationToken);

            throw new BusinessRuleException("That password is not correct.", "invalid_password");
        }

        var now = clock.UtcNow;

        // One transaction across all three writes: an account flagged closed
        // whose refresh tokens survived would keep minting access tokens for
        // data the user has been told is gone.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        await db.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.RevokedAt, (DateTimeOffset?)now)
                    .SetProperty(x => x.RevokedReason, "Account closed")
                    // ExecuteUpdate goes straight to SQL and never reaches the
                    // SaveChanges interceptor, so the audit stamp is set by hand.
                    .SetProperty(x => x.UpdatedAt, (DateTimeOffset?)now),
                cancellationToken);

        // InvalidatedAt stays null on purpose: it records "Expo reported this
        // token as unregistered", and asserting that about a device the user
        // still holds would be a lie written into the row. Clearing IsActive is
        // what actually stops the push sender picking it up.
        await db.DeviceTokens
            .Where(x => x.UserId == userId && x.IsActive)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.IsActive, false)
                    .SetProperty(x => x.UpdatedAt, (DateTimeOffset?)now),
                cancellationToken);

        // Soft delete, never a hard one. The transactions, receipts and budgets
        // hanging off this row are financial records under retention obligations,
        // and the audit trail has to outlive the account it describes — a real
        // DELETE would either cascade that history away or fail outright against
        // the ledger tables' Restrict. The account is closed at the only door
        // that matters instead: AuthService refuses to issue tokens for a user
        // whose IsDeleted is set, so no request can ever carry their identity.
        user.IsDeleted = true;
        user.DeletedAt = now;

        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);

            throw new BusinessRuleException(
                "Your account could not be closed. Please try again.",
                "account_close_failed");
        }

        // Rotating the security stamp invalidates everything derived from it, so
        // the still-unexpired access token this very request arrived on stops
        // being honoured by any check that consults the stamp.
        await userManager.UpdateSecurityStampAsync(user);

        await transaction.CommitAsync(cancellationToken);

        await audit.LogAsync(
            "profile.account_closed",
            userId,
            nameof(ApplicationUser),
            userId,
            succeeded: true,
            cancellationToken: cancellationToken);
    }

    public async Task<string> ExportTransactionsAsync(
        ExportFormat format,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        var query = db.Transactions
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        // Half-open [from, to), like every other range in this system: an
        // inclusive upper bound would export the boundary instant twice when a
        // user pulls two adjacent months.
        if (from is { } start) query = query.Where(x => x.TransactionDate >= start);
        if (to is { } end) query = query.Where(x => x.TransactionDate < end);

        // One row past the cap, so truncation is detected without a second
        // COUNT over the same predicate.
        var rows = await query
            .OrderBy(x => x.TransactionDate)
            .ThenBy(x => x.Id)
            .Take(TransactionExport.MaxRows + 1)
            .Select(ProfileMappings.ToExportRow)
            .ToListAsync(cancellationToken);

        var truncated = rows.Count > TransactionExport.MaxRows;
        if (truncated) rows.RemoveAt(rows.Count - 1);

        var content = format == ExportFormat.Json
            ? BuildJson(rows, from, to, truncated, clock.UtcNow)
            : BuildCsv(rows, await ResolveTimeZoneAsync(userId, cancellationToken), truncated);

        // Bulk egress of an entire ledger is worth a trail entry even when it is
        // the owner doing it to their own data.
        await audit.LogAsync(
            "profile.transactions_exported",
            userId,
            nameof(Transaction),
            entityId: null,
            succeeded: true,
            metadata: new { format = format.ToString(), from, to, rows = rows.Count, truncated },
            cancellationToken: cancellationToken);

        return content;
    }

    private async Task<(ApplicationUser User, IList<string> Roles)> LoadAccountAsync(
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var user = await userManager.FindByIdAsync(userId.ToString());

        // ApplicationUser is exempt from the SoftDelete global filter, so a closed
        // account still comes back here and has to be rejected by hand. The
        // principal came from a token this server signed, so an account that has
        // gone since is a missing resource rather than a failed sign-in.
        if (user is null || user.IsDeleted)
        {
            throw new NotFoundException("Account", userId);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return (user, await userManager.GetRolesAsync(user));
    }

    /// <summary>Applies only the settings the request actually carried.</summary>
    private static void Apply(UserSettings settings, UpdateSettingsRequest request)
    {
        // The column is a fixed-width nchar(3) and every comparison in the system
        // is ordinal, so the case is normalised on the way in rather than at each
        // comparison site.
        if (request.CurrencyCode is { } currencyCode)
        {
            settings.CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        }

        if (request.Locale is { } locale) settings.Locale = locale.Trim();
        if (request.TimeZoneId is { } timeZoneId) settings.TimeZoneId = timeZoneId.Trim();
        if (request.Theme is { } theme) settings.Theme = theme;
        if (request.BudgetAlertsEnabled is { } budgetAlerts) settings.BudgetAlertsEnabled = budgetAlerts;
        if (request.RecurringRemindersEnabled is { } reminders) settings.RecurringRemindersEnabled = reminders;
        if (request.MonthlySummaryEnabled is { } summary) settings.MonthlySummaryEnabled = summary;
        if (request.BudgetWarningThreshold is { } warning) settings.BudgetWarningThreshold = warning;
        if (request.BudgetCriticalThreshold is { } critical) settings.BudgetCriticalThreshold = critical;
        if (request.MonthStartDay is { } monthStartDay) settings.MonthStartDay = monthStartDay;
        if (request.BiometricEnabled is { } biometric) settings.BiometricEnabled = biometric;
    }

    private async Task<TimeZoneInfo> ResolveTimeZoneAsync(Guid userId, CancellationToken cancellationToken)
    {
        var timeZoneId = await db.UserSettings
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken);

        return PeriodCalculator.ResolveTimeZone(timeZoneId);
    }

    // -- Rendering ---------------------------------------------------------

    private static readonly JsonSerializerOptions ExportJson = new(JsonSerializerDefaults.Web)
    {
        // Enums as their names, matching the string unions in api.ts. A bare 2 in
        // a file someone opens a year from now means nothing to them.
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true,
    };

    private static string BuildJson(
        IReadOnlyList<TransactionExportRow> rows,
        DateTimeOffset? from,
        DateTimeOffset? to,
        bool truncated,
        DateTimeOffset generatedAt) =>
        JsonSerializer.Serialize(
            new TransactionExportDocument(generatedAt, from, to, rows.Count, truncated, rows),
            ExportJson);

    /// <summary>Characters that force a field to be quoted, per RFC 4180.</summary>
    private static readonly char[] CsvQuoteTriggers = [',', '"', '\n', '\r'];

    /// <summary>
    /// Leading characters a spreadsheet reads as the start of a formula. A
    /// merchant named <c>=cmd|...!A0</c> is a payload rather than a name, and
    /// Excel evaluates it the moment the file is opened, so a field starting
    /// with one of these is prefixed with an apostrophe that forces it to text.
    /// </summary>
    private const string CsvFormulaTriggers = "=+-@\t\r";

    private static string BuildCsv(IReadOnlyList<TransactionExportRow> rows, TimeZoneInfo timeZone, bool truncated)
    {
        // RFC 4180 says CRLF. Pinning it keeps the file byte-identical whichever
        // OS the server runs on, which Environment.NewLine would not.
        const string NewLine = "\r\n";

        var builder = new StringBuilder(rows.Count * 160);

        // The zone rides in the header because the timestamps below are local
        // wall-clock. A bare "2026-10-01 02:00" is ambiguous, and the alternative
        // — exporting UTC — files an Asia/Kolkata user's late-evening rows under
        // the previous day, the exact complaint PeriodCalculator exists to avoid.
        builder
            .Append(CsvText($"Date ({timeZone.Id})"))
            .Append(",Type,Category,Amount,Currency,Description,Merchant,Payment Method,Notes")
            .Append(NewLine);

        foreach (var row in rows)
        {
            var local = TimeZoneInfo.ConvertTime(row.TransactionDate, timeZone);

            // Date, type, amount and payment method are formatted here out of
            // typed values, so none of them can hold a delimiter, a quote or a
            // formula and all four are appended raw. Putting the amount through
            // the formula guard would be actively wrong: it would quote a
            // negative number into text and break every spreadsheet formula the
            // user then writes over the column. Everything else is user-typed.
            builder
                .Append(local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append(',')
                .Append(row.Type.ToString()).Append(',')
                .Append(CsvText(row.CategoryName)).Append(',')
                // Invariant, not the user's locale: a de-DE machine would render
                // 1.234,56 and split a single amount across two columns.
                .Append(Money.Round(row.Amount).ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(CsvText(row.CurrencyCode)).Append(',')
                .Append(CsvText(row.Description)).Append(',')
                .Append(CsvText(row.Merchant)).Append(',')
                .Append(row.PaymentMethod.ToString()).Append(',')
                .Append(CsvText(row.Notes))
                .Append(NewLine);
        }

        if (truncated)
        {
            // A one-field row rather than a comment line: CSV has no comment
            // syntax, and a bare sentence containing commas would shift columns.
            builder
                .Append(CsvText(
                    $"Export stopped at the first {TransactionExport.MaxRows:N0} transactions. "
                    + "Narrow the date range to export the rest."))
                .Append(NewLine);
        }

        return builder.ToString();
    }

    private static string CsvText(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var text = CsvFormulaTriggers.Contains(value[0]) ? "'" + value : value;

        return text.IndexOfAny(CsvQuoteTriggers) >= 0
            ? string.Concat("\"", text.Replace("\"", "\"\""), "\"")
            : text;
    }
}
