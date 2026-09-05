using ExpenseManagement.Application.Common.Interfaces;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManagement.Infrastructure.Services;

/// <summary>
/// One of exactly two sanctioned cross-user queries in this codebase (the other
/// is <see cref="RecurringScheduleReader"/>).
///
/// It is safe on both methods for the same reason: nothing readable about
/// another person ever leaves it. The fan-out gets opaque token strings for one
/// named account, and the registration lookup returns a single row the caller is
/// immediately taking ownership of — a row whose previous owner has already lost
/// the device it points at.
/// </summary>
public sealed class DeviceTokenReader(AppDbContext db) : IDeviceTokenReader
{
    public async Task<IReadOnlyList<string>> GetActiveTokensAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await db.DeviceTokens
            .IgnoreQueryFilters([FilterNames.UserOwnership])
            .AsNoTracking()
            // With the ownership filter suspended this predicate is the whole of
            // the isolation, not a restatement of it: drop it and a timer job
            // pushes one person's balance alert to every device in the system.
            // IsActive alone is enough to exclude retired tokens because
            // CK_DeviceTokens_Invalidated_Inactive keeps the flag and
            // InvalidatedAt consistent in the database, and the pair matches
            // IX_DeviceTokens_UserId_IsActive exactly, so the read never leaves
            // the index.
            .Where(device => device.UserId == userId && device.IsActive)
            .Select(device => device.Token)
            .ToListAsync(cancellationToken);
    }

    public async Task<DeviceToken?> FindByTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        // Tracked deliberately — AsNoTracking would hand back a detached row the
        // caller could only re-attach by guessing its state. This is the same
        // scoped AppDbContext instance that backs IAppDbContext, so reassigning
        // UserId (or clearing IsActive on a token Expo reported as dead) is
        // persisted by the caller's own SaveChangesAsync.
        return await db.DeviceTokens
            .IgnoreQueryFilters([FilterNames.UserOwnership])
            .FirstOrDefaultAsync(device => device.Token == token, cancellationToken);
    }
}
