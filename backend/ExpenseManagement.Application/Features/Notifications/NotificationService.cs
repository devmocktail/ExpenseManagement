using System.Text.Json;
using ExpenseManagement.Application.Common.Interfaces;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Domain.Enums;
using ExpenseManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManagement.Application.Features.Notifications;

/// <inheritdoc cref="INotificationService"/>
public sealed class NotificationService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IDeviceTokenReader deviceTokens,
    IAuditService audit) : INotificationService
{
    // Mirrors of the column widths in NotificationConfiguration and
    // DeviceTokenConfiguration. Enqueue is called by server code, so no validator
    // stands between those callers and the database.
    private const int TitleMaxLength = 200;
    private const int BodyMaxLength = 1000;
    private const int DeduplicationKeyMaxLength = 200;
    private const int DataJsonMaxLength = 2000;
    private const int TokenMaxLength = 200;
    private const int DeviceNameMaxLength = 100;
    private const int AppVersionMaxLength = 20;

    /// <summary>
    /// The payload is re-emitted to the client verbatim, so it is written with
    /// the same camelCase policy the API serialises everything else with —
    /// otherwise this one object would arrive with PascalCase keys no client
    /// looks up.
    /// </summary>
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<NotificationDto>> ListAsync(
        NotificationQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        // The context's ownership filter scopes this anyway; the explicit
        // predicate states the intent and still holds if an IgnoreQueryFilters
        // call is ever added upstream.
        //
        // The ScheduledFor gate is not cosmetic: a row is written when the event
        // is detected, not when it is due, so a monthly summary queued as the
        // period closes would otherwise be readable hours before the morning it
        // is meant for.
        var query = db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId && n.ScheduledFor <= now);

        if (request.UnreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        return await query
            .OrderByDescending(n => n.CreatedAt)
            // A budget sweep queues several alerts in the same instant; without a
            // tiebreak they swap places between two loads of the same screen.
            .ThenByDescending(n => n.Id)
            .Take(request.Limit)
            .Select(NotificationMappings.ToDto)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        // One statement, no row loaded. The predicate carries no IsRead check on
        // purpose: filtering on it would make "already read" indistinguishable
        // from "not yours" and turn a second tap into a 404. The coalesce keeps
        // the first read time instead, so zero rows affected can only mean the
        // notification is not this user's.
        //
        // CK_Notifications_Read_Consistent pins IsRead and ReadAt together, so
        // both columns have to move in the same statement.
        var affected = await db.Notifications
            .Where(n => n.UserId == userId && n.Id == id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, row => (DateTimeOffset?)(row.ReadAt ?? now))
                    // ExecuteUpdate goes round SaveChanges, so the interceptor
                    // that normally stamps this never runs.
                    .SetProperty(n => n.UpdatedAt, (DateTimeOffset?)now),
                cancellationToken);

        if (affected == 0)
        {
            throw new NotFoundException("Notification", id);
        }
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        // Restricted to unread rows so an account with thousands of notifications
        // rewrites only what changed, and so clearing the badge twice does not
        // reset every ReadAt to now.
        await db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, (DateTimeOffset?)now)
                    .SetProperty(n => n.UpdatedAt, (DateTimeOffset?)now),
                cancellationToken);
    }

    public async Task RegisterDeviceAsync(
        RegisterDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        // The validator rejects a blank token first. This guard exists so a
        // request that somehow reaches the service without it fails as a 4xx
        // rather than as a null dereference on the way to the unique index.
        var token = Normalise(request.Token, TokenMaxLength)
            ?? throw new BusinessRuleException("This device could not be registered for notifications.");

        // Deliberately not scoped to the caller: the token may still be filed
        // under whoever last signed in on this handset.
        var existing = await deviceTokens.FindByTokenAsync(token, cancellationToken);

        if (existing is null)
        {
            var registration = new DeviceToken { Token = token };
            Apply(registration, userId, request, now);

            db.DeviceTokens.Add(registration);

            try
            {
                await db.SaveChangesAsync(cancellationToken);

                await audit.LogAsync(
                    action: "device.registered",
                    userId: userId,
                    entityName: nameof(DeviceToken),
                    entityId: registration.Id,
                    // The token itself never enters the audit trail: it is the
                    // address a push is delivered to, and the trail is read by
                    // more people than should be able to send to it.
                    metadata: new { registration.Platform, registration.DeviceName },
                    cancellationToken: cancellationToken);

                return;
            }
            catch (DbUpdateException exception) when (WasRejected(exception, registration))
            {
                // The app registers on every cold start, so two of those racing
                // both miss the lookup above and collide on UX_DeviceTokens_Token.
                // Remove() on a row still in the Added state detaches it rather
                // than scheduling a delete — the only way to un-queue a failed
                // insert through the IAppDbContext surface — so the update below
                // is not sent alongside the insert that just failed.
                db.DeviceTokens.Remove(registration);

                existing = await deviceTokens.FindByTokenAsync(token, cancellationToken);

                // Not the collision we assumed, so let the real failure surface.
                if (existing is null) throw;
            }
        }

        var previousOwner = existing.UserId;

        Apply(existing, userId, request, now);
        await db.SaveChangesAsync(cancellationToken);

        if (previousOwner != userId)
        {
            // Worth its own trail entry: a token changing hands is the exact event
            // that, done wrong, sends one person's spending alerts to another
            // person's lock screen.
            await audit.LogAsync(
                action: "device.reassigned",
                userId: userId,
                entityName: nameof(DeviceToken),
                entityId: existing.Id,
                metadata: new { previousUserId = previousOwner, existing.Platform },
                cancellationToken: cancellationToken);
        }
    }

    public async Task UnregisterDeviceAsync(string token, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        var normalised = Normalise(token, TokenMaxLength);
        if (normalised is null) return;

        var registration = await db.DeviceTokens
            .Where(d => d.UserId == userId && d.Token == normalised)
            .FirstOrDefaultAsync(cancellationToken);

        // Sign-out calls this best effort while it is already tearing the session
        // down and cannot act on a failure. A token that was never registered — or
        // that has since moved to another account, in which case it is no longer
        // ours to touch — is a no-op rather than a 404. Nothing is disclosed
        // either way: the query never leaves this user's rows.
        if (registration is null) return;

        registration.IsActive = false;

        // InvalidatedAt stays null on purpose. It means "the push service rejected
        // this token", and stamping it here would tell the reaper the installation
        // is dead when the user has merely signed out and may sign back in.
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EnqueueAsync(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        string deduplicationKey,
        object? data,
        DateTimeOffset scheduledFor,
        CancellationToken cancellationToken = default)
    {
        // Programming errors, not user input: this method is only ever called by
        // server-side alerting, so an empty title is a bug that should be loud.
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(deduplicationKey);

        if (!await IsTypeEnabledAsync(userId, type, cancellationToken))
        {
            return;
        }

        var key = Truncate(deduplicationKey, DeduplicationKeyMaxLength);

        // An optimisation, not the guarantee. In a background scope there is no
        // ambient principal, so the ownership filter compares UserId to NULL and
        // this check is blind — which is exactly why the catch below is not
        // optional. Inserts are never filtered, so the unique index still fires.
        var alreadyQueued = await db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId && n.DeduplicationKey == key)
            .AnyAsync(cancellationToken);

        if (alreadyQueued) return;

        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = Truncate(title, TitleMaxLength),
            Body = Truncate(body, BodyMaxLength),
            DataJson = SerialisePayload(data),
            DeduplicationKey = key,
            ScheduledFor = scheduledFor,
        };

        db.Notifications.Add(notification);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (WasRejected(exception, notification))
        {
            // Two sweeps of the same budget racing, or a scheduler re-run after a
            // crash: the unique index picked a winner and this is the loser. The
            // user has already been told, which is what the caller wanted, so this
            // is success. Detaching stops the rejected insert being retried on the
            // caller's next save.
            db.Notifications.Remove(notification);
        }
    }

    /// <summary>
    /// Honours the per-type switches in UserSettings.
    ///
    /// A missing settings row falls through to "notify". The row is created with
    /// the account so that should be unreachable, but it is also what a background
    /// scope sees once the ownership filter hides the row from a query with no
    /// ambient principal — and defaulting to silence there would make alerting
    /// vanish in exactly the context that produces alerts. All three toggles
    /// default to on for a fresh account, so this matches one.
    /// </summary>
    private async Task<bool> IsTypeEnabledAsync(
        Guid userId,
        NotificationType type,
        CancellationToken cancellationToken)
    {
        // Account and security messages are not an opt-out: someone who muted
        // budget alerts still needs to hear that their password was changed.
        if (type == NotificationType.System) return true;

        var toggles = await db.UserSettings
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new
            {
                s.BudgetAlertsEnabled,
                s.RecurringRemindersEnabled,
                s.MonthlySummaryEnabled,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (toggles is null) return true;

        return type switch
        {
            NotificationType.BudgetWarning or NotificationType.BudgetExceeded => toggles.BudgetAlertsEnabled,
            NotificationType.RecurringDue => toggles.RecurringRemindersEnabled,
            NotificationType.MonthlySummary => toggles.MonthlySummaryEnabled,
            _ => true,
        };
    }

    /// <summary>
    /// Moves a registration to the signed-in user and refreshes what the device
    /// reported about itself. Clearing InvalidatedAt beside IsActive is required
    /// rather than tidy: CK_DeviceTokens_Invalidated_Inactive rejects a live row
    /// that still carries a rejection timestamp, and a reinstall genuinely does
    /// revive a token the push service had reported as dead.
    /// </summary>
    private static void Apply(
        DeviceToken registration,
        Guid userId,
        RegisterDeviceRequest request,
        DateTimeOffset now)
    {
        registration.UserId = userId;
        registration.Platform = request.Platform;
        registration.DeviceName = Normalise(request.DeviceName, DeviceNameMaxLength);
        registration.AppVersion = Normalise(request.AppVersion, AppVersionMaxLength);
        registration.IsActive = true;
        registration.InvalidatedAt = null;
        registration.LastSeenAt = now;
    }

    /// <summary>
    /// True when <paramref name="entity"/> is what the failed statement was
    /// writing. Callers batch other work into the same context — the budget whose
    /// threshold tripped is saved by the same unit of work — and swallowing their
    /// failure as "already queued" would leave their change unsaved and
    /// unreported.
    /// </summary>
    private static bool WasRejected(DbUpdateException exception, object entity) =>
        exception.Entries.Any(entry => ReferenceEquals(entry.Entity, entity));

    /// <summary>
    /// Trimmed, or null when nothing is left. Over-long values are clipped rather
    /// than allowed to reach the database, because the catch around each insert
    /// cannot tell a length violation from the duplicate it exists to absorb: a
    /// clipped device name is cosmetic, a silently dropped budget alert is a
    /// feature that stopped working.
    /// </summary>
    private static string? Normalise(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : Truncate(value.Trim(), maxLength);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string? SerialisePayload(object? data)
    {
        if (data is null) return null;

        var json = JsonSerializer.Serialize(data, PayloadOptions);

        // Truncated JSON is JSON that no longer parses, so an oversized payload
        // costs the notification its deep link rather than costing it its row.
        return json.Length <= DataJsonMaxLength ? json : null;
    }
}
