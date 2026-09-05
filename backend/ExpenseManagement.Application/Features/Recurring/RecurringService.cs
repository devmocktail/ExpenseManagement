using System.Globalization;
using ExpenseManagement.Application.Common.Extensions;
using ExpenseManagement.Application.Common.Interfaces;
using ExpenseManagement.Domain.Common;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Domain.Enums;
using ExpenseManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpenseManagement.Application.Features.Recurring;

/// <inheritdoc cref="IRecurringService"/>
public sealed class RecurringService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IRecurringScheduleReader scheduleReader,
    ILogger<RecurringService> logger) : IRecurringService
{
    /// <summary>
    /// Schedules examined per pass. A cap keeps one tick's work — and one
    /// tick's memory — bounded no matter how long the worker was down; whatever
    /// is left over is still due on the next tick.
    /// </summary>
    private const int MaxSchedulesPerPass = 200;

    /// <summary>
    /// Occurrences one schedule may generate in a single pass. A rent schedule
    /// left alone for five years owes sixty transactions; producing them all in
    /// one go would be a surprise the user cannot undo in one go either.
    /// </summary>
    private const int MaxOccurrencesPerPass = 12;

    public async Task<IReadOnlyList<RecurringDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        return await db.RecurringTransactions
            .AsNoTracking()
            .Where(schedule => schedule.UserId == userId)
            // Matches IX_RecurringTransactions_UserId_IsPaused, which is also the
            // order the screen renders: live schedules, soonest first, then paused.
            .OrderBy(schedule => schedule.IsPaused)
            .ThenBy(schedule => schedule.NextRunDate)
            .ThenBy(schedule => schedule.Name)
            .Select(RecurringMappings.ToDto)
            .ToListAsync(cancellationToken);
    }

    public async Task<RecurringDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();
        return await ProjectAsync(userId, id, cancellationToken);
    }

    public async Task<RecurringDto> CreateAsync(
        CreateRecurringRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();
        var settings = await LoadSettingsAsync(userId, cancellationToken);

        await EnsureCategoryIsUsableAsync(userId, request.CategoryId, request.Type, cancellationToken);

        var interval = request.Interval ?? 1;
        var now = clock.UtcNow;

        // A start date in the past is normal — users enter the rent they have been
        // paying for years — so the series is anchored there for its day-of-month
        // but the first run is the next occurrence that has not happened yet.
        var nextRun = request.StartDate > now
            ? request.StartDate
            : RecurrenceCalculator.FirstOnOrAfter(request.StartDate, now, request.Frequency, interval, settings.TimeZone);

        if (request.EndDate is { } endDate && nextRun > endDate)
        {
            throw new BusinessRuleException(
                "This schedule would end before its next occurrence. Choose a later end date.",
                "recurring_already_exhausted");
        }

        var schedule = new RecurringTransaction
        {
            UserId = userId,
            CategoryId = request.CategoryId,
            Name = request.Name.Trim(),
            Type = request.Type,
            Amount = Money.Round(request.Amount),

            // Not client-supplied: the generated transactions have to be summable
            // against every other row in the account, which they are not if a
            // schedule can mint its own currency.
            CurrencyCode = settings.CurrencyCode,

            PaymentMethod = request.PaymentMethod,
            Merchant = Normalise(request.Merchant),
            Description = Normalise(request.Description),
            Frequency = request.Frequency,
            Interval = interval,
            // Normalised to UTC on the way in. The column is datetimeoffset and
            // preserves whatever offset the device sent, which range predicates
            // handle correctly but the analytics trend does not: it buckets with
            // DATEADD/DATEPART on the stored local portion, so a non-zero offset
            // is applied a second time and the money lands on the wrong day.
            StartDate = request.StartDate.ToUniversalTime(),
            EndDate = request.EndDate?.ToUniversalTime(),
            NextRunDate = nextRun.ToUniversalTime(),
            IsPaused = false,
            ReminderDaysBefore = request.ReminderDaysBefore ?? 1,
        };

        db.RecurringTransactions.Add(schedule);
        await db.SaveChangesAsync(cancellationToken);

        return await ProjectAsync(userId, schedule.Id, cancellationToken);
    }

    public async Task<RecurringDto> UpdateAsync(
        Guid id,
        UpdateRecurringRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();
        var settings = await LoadSettingsAsync(userId, cancellationToken);

        // Tracked on purpose: this is a load-for-mutation, not a read.
        var schedule = await db.RecurringTransactions
            .Where(s => s.Id == id && s.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(RecurringTransaction), id);

        await EnsureCategoryIsUsableAsync(userId, request.CategoryId, request.Type, cancellationToken);

        var interval = request.Interval ?? 1;

        // Only a change to the recurrence itself may move the run date. Repricing a
        // subscription must not silently re-date the next charge, and recomputing
        // unconditionally would do exactly that on every edit.
        var recurrenceMoved =
            schedule.StartDate != request.StartDate
            || schedule.Frequency != request.Frequency
            || schedule.Interval != interval;

        schedule.CategoryId = request.CategoryId;
        schedule.Name = request.Name.Trim();
        schedule.Type = request.Type;
        schedule.Amount = Money.Round(request.Amount);
        schedule.PaymentMethod = request.PaymentMethod;
        schedule.Merchant = Normalise(request.Merchant);
        schedule.Description = Normalise(request.Description);
        schedule.Frequency = request.Frequency;
        schedule.Interval = interval;
        schedule.StartDate = request.StartDate.ToUniversalTime();
        schedule.EndDate = request.EndDate?.ToUniversalTime();
        schedule.ReminderDaysBefore = request.ReminderDaysBefore ?? schedule.ReminderDaysBefore;

        if (recurrenceMoved)
        {
            var now = clock.UtcNow;

            schedule.NextRunDate = (request.StartDate > now
                ? request.StartDate
                : RecurrenceCalculator.FirstOnOrAfter(request.StartDate, now, request.Frequency, interval, settings.TimeZone))
                .ToUniversalTime();
        }

        // Shortening a series until nothing is left is a legitimate way to end it,
        // so unlike create this is not rejected: IsDueAt already refuses to generate
        // past EndDate, and the schedule stays visible as the record of what ran.
        await db.SaveChangesAsync(cancellationToken);

        return await ProjectAsync(userId, id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        var schedule = await db.RecurringTransactions
            .Where(s => s.Id == id && s.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(RecurringTransaction), id);

        // The context turns Remove into a soft delete. The transactions this
        // schedule already generated are separate rows and keep their history; only
        // the template stops existing.
        db.RecurringTransactions.Remove(schedule);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<RecurringDto> SetPausedAsync(
        Guid id,
        bool isPaused,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        var schedule = await db.RecurringTransactions
            .Where(s => s.Id == id && s.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(RecurringTransaction), id);

        var now = clock.UtcNow;

        // Resuming skips whatever came due during the pause instead of generating
        // it. A pause means "this was not charged", so back-filling three months of
        // a paused subscription would invent spending that never happened.
        if (schedule.IsPaused && !isPaused && schedule.NextRunDate <= now)
        {
            var timeZone = await LoadTimeZoneAsync(userId, cancellationToken);

            schedule.NextRunDate = RecurrenceCalculator.FirstOnOrAfter(
                schedule.StartDate,
                now,
                schedule.Frequency,
                schedule.Interval,
                timeZone);
        }

        schedule.IsPaused = isPaused;
        await db.SaveChangesAsync(cancellationToken);

        return await ProjectAsync(userId, id, cancellationToken);
    }

    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        var dueIds = await scheduleReader.GetDueScheduleIdsAsync(now, MaxSchedulesPerPass, cancellationToken);
        if (dueIds.Count == 0) return 0;

        // One zone lookup per account, not per schedule: a user with a dozen
        // subscriptions would otherwise re-read the same settings row a dozen times.
        var timeZones = new Dictionary<Guid, TimeZoneInfo>();
        var generated = 0;

        foreach (var scheduleId in dueIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            generated += await GenerateForScheduleAsync(scheduleId, now, timeZones, cancellationToken);
        }

        if (dueIds.Count == MaxSchedulesPerPass)
        {
            logger.LogInformation(
                "Recurring pass filled its schedule budget of {Max}; the remaining due schedules run on the next tick.",
                MaxSchedulesPerPass);
        }

        return generated;
    }

    /// <summary>
    /// Generates everything one schedule owes, up to the per-pass cap. Returns
    /// the number of transactions actually inserted, which is not the number of
    /// occurrences advanced past: an occurrence a previous, interrupted pass
    /// already banked is skipped, not counted, and not duplicated.
    /// </summary>
    private async Task<int> GenerateForScheduleAsync(
        Guid scheduleId,
        DateTimeOffset now,
        Dictionary<Guid, TimeZoneInfo> timeZones,
        CancellationToken cancellationToken)
    {
        // The reader hands over ids from every account, so this load has to see
        // past the ownership filter — there is no authenticated user in a worker,
        // and the filter's parameter is null, which matches nothing. The named form
        // (IgnoreQueryFilters([FilterNames.UserOwnership])) needs a constant that
        // lives in Infrastructure, so soft-delete is instead re-applied by hand
        // below and a schedule deleted since the scan is skipped, not resurrected.
        var schedule = await db.RecurringTransactions
            .IgnoreQueryFilters()
            .Where(s => s.Id == scheduleId && !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (schedule is null) return 0;

        var timeZone = await ResolveTimeZoneAsync(schedule.UserId, timeZones, cancellationToken);

        var generated = 0;
        var processed = 0;

        while (processed < MaxOccurrencesPerPass && schedule.IsDueAt(now))
        {
            var occurrence = schedule.NextRunDate;
            var reference = ClientReferenceFor(schedule.Id, occurrence);

            // Kept so the in-memory schedule can be put back exactly as the database
            // still has it if this occurrence fails.
            var previousLastRun = schedule.LastRunDate;
            var previousCount = schedule.OccurrencesGenerated;
            var previousPaused = schedule.IsPaused;

            Transaction? pending = null;
            var inserted = false;

            await using var dbTransaction = await db.BeginTransactionAsync(cancellationToken);

            try
            {
                // A row already carrying this reference means an earlier pass inserted
                // the occurrence and died before advancing the schedule. Advancing over
                // it is the whole point of the deterministic reference: replaying is a
                // no-op instead of a second charge.
                var alreadyBanked = await db.Transactions
                    .IgnoreQueryFilters()
                    .AnyAsync(
                        t => t.UserId == schedule.UserId && t.ClientReference == reference && !t.IsDeleted,
                        cancellationToken);

                if (!alreadyBanked)
                {
                    pending = BuildTransaction(schedule, occurrence, reference);
                    db.Transactions.Add(pending);

                    // Saved before the schedule moves, inside the same transaction: the
                    // row exists before anything claims the occurrence is done, and the
                    // pair commits or rolls back together.
                    await db.SaveChangesAsync(cancellationToken);
                    inserted = true;
                }

                Advance(schedule, occurrence, timeZone);
                await db.SaveChangesAsync(cancellationToken);
                await dbTransaction.CommitAsync(cancellationToken);

                if (inserted) generated++;
                processed++;
            }
            catch (DbUpdateException ex)
            {
                await dbTransaction.RollbackAsync(cancellationToken);

                // Rolling back the database does not roll back the change tracker, and
                // this context goes on to serve the rest of the pass. An unsaved insert
                // left in it would be re-attempted by the next schedule's save, and a
                // half-advanced schedule would be committed by it, so both are undone
                // by hand. Remove on an Added entity detaches it rather than deleting.
                if (pending is not null && !inserted)
                {
                    db.Transactions.Remove(pending);
                }

                schedule.NextRunDate = occurrence;
                schedule.LastRunDate = previousLastRun;
                schedule.OccurrencesGenerated = previousCount;
                schedule.IsPaused = previousPaused;

                // Losing a race with a second worker instance lands here, on the unique
                // client-reference index. Abandoning this schedule rather than guessing
                // which write failed is safe: the next pass sees whatever did commit and
                // advances over it.
                logger.LogError(
                    ex,
                    "Recurring schedule {ScheduleId} failed to generate its occurrence due at {Occurrence}; it will be retried on the next pass.",
                    schedule.Id,
                    occurrence);

                break;
            }
        }

        if (processed == MaxOccurrencesPerPass && schedule.IsDueAt(now))
        {
            logger.LogWarning(
                "Recurring schedule {ScheduleId} hit the catch-up cap of {Cap} occurrences and is still overdue at {NextRunDate}.",
                schedule.Id,
                MaxOccurrencesPerPass,
                schedule.NextRunDate);
        }

        return generated;
    }

    private static Transaction BuildTransaction(
        RecurringTransaction schedule,
        DateTimeOffset occurrence,
        string clientReference) => new()
        {
            // Taken from the schedule row, never from anything a caller supplied:
            // this code path runs with no principal at all.
            UserId = schedule.UserId,
            CategoryId = schedule.CategoryId,
            Type = schedule.Type,
            Amount = Money.Round(schedule.Amount),
            CurrencyCode = schedule.CurrencyCode,

            // Falls back to the schedule's name so a generated row is never a blank
            // line in the ledger the user cannot place a month later.
            Description = schedule.Description ?? schedule.Name,
            Merchant = schedule.Merchant,
            PaymentMethod = schedule.PaymentMethod,

            // Dated to when the money was due, not to when the worker got round to
            // it, so a late pass cannot file last month's rent under this month.
            TransactionDate = occurrence.ToUniversalTime(),

            RecurringTransactionId = schedule.Id,
            ClientReference = clientReference,
        };

    /// <summary>
    /// Moves the schedule past the occurrence just generated. Always computed
    /// from <see cref="RecurringTransaction.StartDate"/> rather than from the
    /// occurrence, so a monthly series anchored on the 31st returns to the 31st
    /// after every short month instead of settling on the 28th.
    /// </summary>
    private static void Advance(RecurringTransaction schedule, DateTimeOffset occurrence, TimeZoneInfo timeZone)
    {
        schedule.LastRunDate = occurrence;
        schedule.OccurrencesGenerated++;

        var next = RecurrenceCalculator.Next(
            schedule.StartDate,
            occurrence,
            schedule.Frequency,
            schedule.Interval,
            timeZone);

        schedule.NextRunDate = next;

        // Exhausted. Pausing is what retires it: the scheduler's index is filtered
        // on IsPaused = 0, so a finished schedule stops being scanned every tick
        // while the user keeps the record of what it generated.
        if (schedule.EndDate is { } endDate && next > endDate)
        {
            schedule.IsPaused = true;
        }
    }

    /// <summary>
    /// The idempotency key that makes a re-run harmless. Deterministic in the
    /// schedule and the occurrence instant, normalised to UTC so the same moment
    /// can never render as two different strings, and 80 characters at its
    /// longest — inside the column's 100 and the unique index built on it.
    /// </summary>
    private static string ClientReferenceFor(Guid scheduleId, DateTimeOffset occurrence) =>
        $"recurring:{scheduleId:D}:{occurrence.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)}";

    private async Task<RecurringDto> ProjectAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        await db.RecurringTransactions
            .AsNoTracking()
            .Where(schedule => schedule.Id == id && schedule.UserId == userId)
            .Select(RecurringMappings.ToDto)
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(RecurringTransaction), id);

    private async Task EnsureCategoryIsUsableAsync(
        Guid userId,
        Guid categoryId,
        TransactionType type,
        CancellationToken cancellationToken)
    {
        var categoryType = await db.Categories
            .AsNoTracking()
            .Where(category => category.Id == categoryId && category.UserId == userId)
            .Select(category => (TransactionType?)category.Type)
            .FirstOrDefaultAsync(cancellationToken);

        // Another account's category has to read as missing rather than forbidden:
        // a 403 here would confirm the id exists and make this an enumeration oracle.
        if (categoryType is null)
        {
            throw new NotFoundException(nameof(Category), categoryId);
        }

        // Enforced now instead of at generation time, because a mismatch discovered
        // by the worker has nobody to report it to and would keep failing nightly.
        if (categoryType != type)
        {
            throw new BusinessRuleException(
                type == TransactionType.Expense
                    ? "Choose an expense category for a recurring expense."
                    : "Choose an income category for recurring income.",
                "recurring_category_type_mismatch");
        }
    }

    private async Task<RecurringSettings> LoadSettingsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var settings = await db.UserSettings
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new { s.TimeZoneId, s.CurrencyCode })
            .FirstOrDefaultAsync(cancellationToken);

        // The settings row is created with the account, so this fallback should be
        // unreachable — but a schedule that runs in UTC beats a 500 on a save.
        return settings is null
            ? new RecurringSettings(TimeZoneInfo.Utc, "INR")
            : new RecurringSettings(PeriodCalculator.ResolveTimeZone(settings.TimeZoneId), settings.CurrencyCode);
    }

    private async Task<TimeZoneInfo> LoadTimeZoneAsync(Guid userId, CancellationToken cancellationToken) =>
        (await LoadSettingsAsync(userId, cancellationToken)).TimeZone;

    /// <summary>
    /// The schedule owner's zone, read once per account per pass. UserSettings is
    /// user-owned but not soft-deletable, so the ownership filter is the only one
    /// suspended here — and the row is still pinned to one explicit user id.
    /// </summary>
    private async Task<TimeZoneInfo> ResolveTimeZoneAsync(
        Guid userId,
        Dictionary<Guid, TimeZoneInfo> cache,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(userId, out var cached)) return cached;

        var timeZoneId = await db.UserSettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken);

        var timeZone = PeriodCalculator.ResolveTimeZone(timeZoneId);
        cache[userId] = timeZone;

        return timeZone;
    }

    /// <summary>Empty strings and whitespace are stored as null so "no merchant" has one representation.</summary>
    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record RecurringSettings(TimeZoneInfo TimeZone, string CurrencyCode);
}
