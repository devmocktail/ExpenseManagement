using ExpenseManagement.Domain.Common;
using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Domain.Entities;

/// <summary>
/// Per-user preferences. One row per user, created alongside the account so
/// the client never has to cope with a missing settings record.
/// </summary>
public class UserSettings : BaseEntity, IUserOwnedEntity
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    /// <summary>ISO 4217 code, e.g. INR / USD / EUR. Never assume a symbol from this in the domain.</summary>
    public string CurrencyCode { get; set; } = "INR";

    /// <summary>BCP 47 tag used for number and date formatting on the client.</summary>
    public string Locale { get; set; } = "en-IN";

    /// <summary>IANA zone, e.g. Asia/Kolkata. All timestamps are stored UTC and rendered in this zone.</summary>
    public string TimeZoneId { get; set; } = "Asia/Kolkata";

    public ThemePreference Theme { get; set; } = ThemePreference.System;

    public bool BudgetAlertsEnabled { get; set; } = true;
    public bool RecurringRemindersEnabled { get; set; } = true;
    public bool MonthlySummaryEnabled { get; set; } = true;

    /// <summary>Percent of a budget at which the first warning fires. 1-100.</summary>
    public int BudgetWarningThreshold { get; set; } = 75;

    /// <summary>Percent of a budget at which the second, louder warning fires. 1-100.</summary>
    public int BudgetCriticalThreshold { get; set; } = 90;

    /// <summary>Day of month a monthly budget period starts on (1-28), for users paid mid-month.</summary>
    public int MonthStartDay { get; set; } = 1;

    public bool BiometricEnabled { get; set; }
}
