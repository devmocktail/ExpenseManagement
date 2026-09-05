using System.Linq.Expressions;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Application.Features.Profile;

// Request records use init-only properties rather than positional parameters,
// for the reason spelled out in AuthDtos: an absent JSON member leaves the
// property at its default instead of failing to bind, and a validator turns
// that into a field-level message the client can render.
//
// The profile response is not redeclared here. `UserProfile` in
// mobile/src/types/api.ts is one wire type, and it is already modelled by
// Features.Auth.UserProfileDto because login has to return it too. A second C#
// record of the same shape would drift the moment one of them gains a field.

/// <summary>Wire shape of <c>UpdateProfileRequest</c>.</summary>
public sealed record UpdateProfileRequest
{
    public string FullName { get; init; } = string.Empty;
}

/// <summary>Wire shape of <c>UserSettings</c>. Property order follows the TypeScript type.</summary>
public sealed record UserSettingsDto(
    string CurrencyCode,
    string Locale,
    string TimeZoneId,
    ThemePreference Theme,
    bool BudgetAlertsEnabled,
    bool RecurringRemindersEnabled,
    bool MonthlySummaryEnabled,
    int BudgetWarningThreshold,
    int BudgetCriticalThreshold,
    int MonthStartDay,
    bool BiometricEnabled);

/// <summary>
/// Wire shape of <c>UpdateSettingsRequest</c>, which is <c>Partial&lt;UserSettings&gt;</c>.
///
/// Every property is nullable because the route is a PUT the client uses as a
/// PATCH: the settings screen sends only the switch that moved. Null therefore
/// means "leave this alone", which works here only because no setting is itself
/// nullable — if one ever becomes nullable it needs its own presence flag
/// rather than sharing this one's meaning of null.
/// </summary>
public sealed record UpdateSettingsRequest
{
    public string? CurrencyCode { get; init; }

    public string? Locale { get; init; }

    public string? TimeZoneId { get; init; }

    public ThemePreference? Theme { get; init; }

    public bool? BudgetAlertsEnabled { get; init; }

    public bool? RecurringRemindersEnabled { get; init; }

    public bool? MonthlySummaryEnabled { get; init; }

    public int? BudgetWarningThreshold { get; init; }

    public int? BudgetCriticalThreshold { get; init; }

    public int? MonthStartDay { get; init; }

    public bool? BiometricEnabled { get; init; }
}

/// <summary>
/// Not a named type in <c>api.ts</c> — the client sends a bare
/// <c>{ password }</c> body with <c>DELETE /profile</c>, which is this shape.
/// The password is here to re-authenticate the caller, never to change one.
/// </summary>
public sealed record DeleteAccountRequest
{
    public string Password { get; init; } = string.Empty;
}

/// <summary>
/// Wire values are the lowercase <c>'csv' | 'json'</c> of <c>ExportFormat</c>.
/// Nothing extra is needed to bind them: the format arrives as a query-string
/// value and ASP.NET Core's enum binding is case-insensitive.
/// </summary>
public enum ExportFormat : byte
{
    Csv = 1,
    Json = 2,
}

/// <summary>
/// Wire shape of <c>ExportRequest</c>, bound from the query string.
///
/// The service takes these three values as parameters rather than taking this
/// record, so a scheduled job can export without constructing a request DTO;
/// the record exists so the endpoint has something to bind and validate.
/// </summary>
public sealed record ExportTransactionsRequest
{
    public ExportFormat Format { get; init; } = ExportFormat.Csv;

    /// <summary>Inclusive lower bound, UTC. Null means "from the first transaction".</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Exclusive upper bound, UTC — see <c>DateRange</c> on why ends are exclusive.</summary>
    public DateTimeOffset? To { get; init; }
}

/// <summary>
/// One exported transaction. Flatter and narrower than <c>TransactionDto</c> on
/// purpose: an export has no use for receipts or category styling, and pulling
/// the receipt collection would add a join per row to a query that already
/// reads thousands of them.
/// </summary>
public sealed record TransactionExportRow(
    Guid Id,
    DateTimeOffset TransactionDate,
    TransactionType Type,
    string CategoryName,
    decimal Amount,
    string CurrencyCode,
    string? Description,
    string? Merchant,
    PaymentMethod PaymentMethod,
    string? Notes);

/// <summary>
/// The JSON export document. It is an object rather than a bare array so the
/// range, the row count and — crucially — the truncation flag travel with the
/// data; a bare array that silently stopped at the cap would look complete.
/// </summary>
public sealed record TransactionExportDocument(
    DateTimeOffset ExportedAt,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Count,
    bool Truncated,
    IReadOnlyList<TransactionExportRow> Transactions);

/// <summary>Facts about an export that both the service and the endpoint need.</summary>
public static class TransactionExport
{
    /// <summary>
    /// Ceiling on rows in one export. An export is built entirely in memory as a
    /// string, so this is what stops one request with no date filter from
    /// allocating a multi-hundred-megabyte buffer per caller.
    /// </summary>
    public const int MaxRows = 10_000;

    public static string ContentType(ExportFormat format) =>
        format == ExportFormat.Json ? "application/json" : "text/csv";

    /// <summary>Suggested <c>Content-Disposition</c> name, so every endpoint spells it the same way.</summary>
    public static string FileName(ExportFormat format, DateTimeOffset generatedAt) =>
        $"transactions-{generatedAt:yyyy-MM-dd}.{(format == ExportFormat.Json ? "json" : "csv")}";
}

/// <summary>
/// The shaping this feature shares between its queries and its in-memory paths.
/// </summary>
public static class ProfileMappings
{
    /// <summary>
    /// Held in a get-only property so the tree is built once per process rather
    /// than rebuilt per request.
    /// </summary>
    public static Expression<Func<UserSettings, UserSettingsDto>> ToSettingsDto { get; } = s => new UserSettingsDto(
        s.CurrencyCode,
        s.Locale,
        s.TimeZoneId,
        s.Theme,
        s.BudgetAlertsEnabled,
        s.RecurringRemindersEnabled,
        s.MonthlySummaryEnabled,
        s.BudgetWarningThreshold,
        s.BudgetCriticalThreshold,
        s.MonthStartDay,
        s.BiometricEnabled);

    /// <summary>
    /// The same projection, compiled once, for the update path — that one has
    /// just mutated a tracked entity and has nothing left to run in SQL.
    /// Compiling the shared expression instead of hand-writing a second mapper
    /// is what stops a new setting from appearing on read and vanishing on write.
    /// </summary>
    public static Func<UserSettings, UserSettingsDto> ToSettings { get; } = ToSettingsDto.Compile();

    public static Expression<Func<Transaction, TransactionExportRow>> ToExportRow { get; } = t => new TransactionExportRow(
        t.Id,
        t.TransactionDate,
        t.Type,
        t.Category.Name,
        t.Amount,
        t.CurrencyCode,
        t.Description,
        t.Merchant,
        t.PaymentMethod,
        t.Notes);
}
