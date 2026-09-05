using System.Linq.Expressions;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Application.Features.Recurring;

/// <summary>
/// A schedule as the client renders it. The category's display fields are
/// flattened onto the row so the "Recurring" screen paints without a second
/// lookup per schedule, and the run bookkeeping (<see cref="NextRunDate"/>,
/// <see cref="OccurrencesGenerated"/>) is exposed read-only — those values are
/// the scheduler's, and a client that could set them could bill an account
/// twice.
/// </summary>
public sealed record RecurringDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required TransactionType Type { get; init; }
    public required decimal Amount { get; init; }
    public required string CurrencyCode { get; init; }

    public required Guid CategoryId { get; init; }
    public required string CategoryName { get; init; }
    public required string CategoryIcon { get; init; }
    public required string CategoryColor { get; init; }

    public required PaymentMethod PaymentMethod { get; init; }
    public string? Merchant { get; init; }
    public string? Description { get; init; }

    public required RecurrenceFrequency Frequency { get; init; }

    /// <summary>Every <c>Interval</c> units of <see cref="Frequency"/>: 2 + Weekly is fortnightly.</summary>
    public required int Interval { get; init; }

    public required DateTimeOffset StartDate { get; init; }

    /// <summary>Null runs until the user pauses or deletes the schedule.</summary>
    public DateTimeOffset? EndDate { get; init; }

    public required DateTimeOffset NextRunDate { get; init; }
    public DateTimeOffset? LastRunDate { get; init; }
    public required int OccurrencesGenerated { get; init; }

    public required bool IsPaused { get; init; }
    public required int ReminderDaysBefore { get; init; }
}

/// <summary>
/// What create and update have in common — which here is everything. Stated
/// once so the two endpoints cannot accept different things and so the rules in
/// <c>RecurringWriteRequestValidator</c> are written once.
/// </summary>
public interface IRecurringWriteRequest
{
    string Name { get; }
    TransactionType Type { get; }
    decimal Amount { get; }
    Guid CategoryId { get; }
    PaymentMethod PaymentMethod { get; }
    RecurrenceFrequency Frequency { get; }
    int? Interval { get; }
    DateTimeOffset StartDate { get; }
    DateTimeOffset? EndDate { get; }
    string? Merchant { get; }
    string? Description { get; }
    int? ReminderDaysBefore { get; }
}

/// <summary>
/// A new schedule.
///
/// No member is <c>required</c> on purpose: System.Text.Json rejects a payload
/// with a missing required property before FluentValidation ever runs, which
/// turns a friendly per-field message into an opaque parse failure. There is no
/// currency here either — a schedule is always denominated in the account's
/// currency, so the transactions it generates can be summed with every other.
/// </summary>
public sealed record CreateRecurringRequest : IRecurringWriteRequest
{
    public string Name { get; init; } = string.Empty;
    public TransactionType Type { get; init; } = TransactionType.Expense;
    public decimal Amount { get; init; }
    public Guid CategoryId { get; init; }
    public PaymentMethod PaymentMethod { get; init; } = PaymentMethod.Cash;
    public RecurrenceFrequency Frequency { get; init; } = RecurrenceFrequency.Monthly;

    /// <summary>Null means every period.</summary>
    public int? Interval { get; init; }

    public DateTimeOffset StartDate { get; init; }
    public DateTimeOffset? EndDate { get; init; }
    public string? Merchant { get; init; }
    public string? Description { get; init; }

    /// <summary>Null keeps the current setting; 0 turns the reminder off.</summary>
    public int? ReminderDaysBefore { get; init; }
}

/// <summary>Identical to the create body — a schedule is edited whole.</summary>
public sealed record UpdateRecurringRequest : IRecurringWriteRequest
{
    public string Name { get; init; } = string.Empty;
    public TransactionType Type { get; init; } = TransactionType.Expense;
    public decimal Amount { get; init; }
    public Guid CategoryId { get; init; }
    public PaymentMethod PaymentMethod { get; init; } = PaymentMethod.Cash;
    public RecurrenceFrequency Frequency { get; init; } = RecurrenceFrequency.Monthly;
    public int? Interval { get; init; }
    public DateTimeOffset StartDate { get; init; }
    public DateTimeOffset? EndDate { get; init; }
    public string? Merchant { get; init; }
    public string? Description { get; init; }
    public int? ReminderDaysBefore { get; init; }
}

/// <summary>
/// Body of <c>PATCH /recurring/{id}/paused</c>. Pausing is its own endpoint
/// rather than a field on the update body because it is the one change the user
/// makes in a hurry, and it must not require sending — and therefore risk
/// overwriting — the whole schedule.
/// </summary>
public sealed record SetRecurringPausedRequest
{
    public bool IsPaused { get; init; }
}

/// <summary>
/// The single projection every recurring read goes through: list, detail and
/// the read-back after a write. One expression means a field added to the DTO
/// appears in all of them, and none of them can drift into loading entities and
/// mapping in memory.
/// </summary>
public static class RecurringMappings
{
    /// <summary>
    /// A get-only property so the tree is built once per process instead of once
    /// per request; EF caches the compiled plan by shape either way.
    /// </summary>
    public static Expression<Func<RecurringTransaction, RecurringDto>> ToDto { get; } = schedule => new RecurringDto
    {
        Id = schedule.Id,
        Name = schedule.Name,
        Type = schedule.Type,
        Amount = schedule.Amount,
        CurrencyCode = schedule.CurrencyCode,

        CategoryId = schedule.CategoryId,
        CategoryName = schedule.Category.Name,
        CategoryIcon = schedule.Category.Icon,
        CategoryColor = schedule.Category.Color,

        PaymentMethod = schedule.PaymentMethod,
        Merchant = schedule.Merchant,
        Description = schedule.Description,

        Frequency = schedule.Frequency,
        Interval = schedule.Interval,
        StartDate = schedule.StartDate,
        EndDate = schedule.EndDate,

        NextRunDate = schedule.NextRunDate,
        LastRunDate = schedule.LastRunDate,
        OccurrencesGenerated = schedule.OccurrencesGenerated,

        IsPaused = schedule.IsPaused,
        ReminderDaysBefore = schedule.ReminderDaysBefore,
    };
}
