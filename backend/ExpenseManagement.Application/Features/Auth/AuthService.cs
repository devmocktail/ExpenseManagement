using ExpenseManagement.Application.Common.Interfaces;
using ExpenseManagement.Application.Features.Categories;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Domain.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpenseManagement.Application.Features.Auth;

/// <summary>
/// Registration, sign-in and the refresh-token lifecycle.
///
/// Two things are worth knowing before reading further.
///
/// First, there is no <c>SignInManager</c> here even though it is the obvious
/// tool for <c>CheckPasswordSignInAsync</c>. That type lives in the
/// <c>Microsoft.AspNetCore.Identity</c> assembly, which ships in the ASP.NET
/// Core shared framework; this project references only
/// <c>Microsoft.Extensions.Identity.Core</c>, which carries
/// <c>UserManager</c>, <c>RoleManager</c> and <c>IdentityResult</c> but not
/// <c>SignInManager</c>. Taking a dependency on it would either break the build
/// or drag a <c>FrameworkReference</c> into the application layer and let web
/// concerns leak in behind it. The password path below reproduces
/// <c>CheckPasswordSignInAsync(..., lockoutOnFailure: true)</c> exactly out of
/// the <c>UserManager</c> primitives it is built from: lockout check, password
/// check, then advance or reset the failure counter.
///
/// Second, refresh tokens are read on an anonymous endpoint, where
/// <c>AppDbContext.CurrentUserId</c> is null and the <c>UserOwnership</c> global
/// filter therefore matches nothing. Those reads suspend that one filter by name
/// and take the owner from the token row itself, which is server state.
/// </summary>
public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IAppDbContext db,
    ICurrentUser currentUser,
    ITokenService tokenService,
    IDateTimeProvider clock,
    IAuditService audit,
    IEmailSender emailSender,
    ILogger<AuthService> logger) : IAuthService
{
    /// <summary>
    /// Mirrors <c>FilterNames.UserOwnership</c> in the infrastructure layer.
    /// Duplicated as a literal because the application layer must not reference
    /// infrastructure; both sides are a single greppable token if it ever moves.
    /// </summary>
    private const string UserOwnershipFilter = "UserOwnership";

    private const string InvalidCredentialsMessage = "That email address or password is not correct.";

    private const string AccountLockedMessage =
        "Too many failed sign-in attempts. This account is locked for a short while — please try again later.";

    private const string SessionEndedMessage = "Your session is no longer valid. Please sign in again.";

    private const string ResetFailedMessage =
        "That reset link is no longer valid. Please request a new one.";

    /// <summary>
    /// A real password hash of a throwaway value, produced once per process by
    /// the configured hasher. See <see cref="BurnPasswordVerification"/>.
    /// </summary>
    private static string? _decoyPasswordHash;

    public async Task<AuthResponseDto> RegisterAsync(
        RegisterRequest request,
        ClientContext? client = null,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim();

        // Identity re-checks this inside CreateAsync, but checking first keeps the
        // failure a 409 with a sentence a person can act on, rather than a generic
        // identity error raised after a transaction is already open. A closed
        // account still holds its address here on purpose — see the note in
        // ApplicationUserConfiguration about not letting an address be re-registered
        // while the old account's financial history is still on disk.
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw new ConflictException("An account already exists for that email address.");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
        };

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw Translate(created, "registration_failed");
        }

        await EnsureUserRoleExistsAsync();

        var roleAssigned = await userManager.AddToRoleAsync(user, RoleNames.User);
        if (!roleAssigned.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw Translate(roleAssigned, "registration_failed");
        }

        db.UserSettings.Add(BuildSettings(user.Id, request));

        // Every user gets their own copy of the canonical set, so nothing they do
        // to a category can ever touch another account.
        db.Categories.AddRange(DefaultCategories.All.Select(template => new Category
        {
            UserId = user.Id,
            Name = template.Name,
            Type = template.Type,
            Icon = template.Icon,
            Color = template.Color,
            SortOrder = template.SortOrder,
            IsSystem = true,
        }));

        // The session is minted inside the transaction too: an account that exists
        // but cannot be signed into would be exactly the half-registered state this
        // transaction is here to prevent.
        var roles = await userManager.GetRolesAsync(user);
        var (tokens, _) = IssueTokens(user, roles, familyId: null, client);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // The address is deliberately not in the metadata: the audit trail answers
        // "who did what, when", and the user id already answers "who".
        await audit.LogAsync(
            "auth.register",
            user.Id,
            nameof(ApplicationUser),
            user.Id,
            succeeded: true,
            cancellationToken: cancellationToken);

        try
        {
            await emailSender.SendWelcomeAsync(user.Email!, user.FullName, cancellationToken);
        }
        catch (Exception ex)
        {
            // The account is committed and the caller is holding valid tokens; a mail
            // outage must not turn that into a failure they would retry into a 409.
            logger.LogWarning(ex, "Welcome email could not be sent for user {UserId}.", user.Id);
        }

        return AuthMappings.ToAuthResponse(tokens, AuthMappings.ToProfile(user, roles));
    }

    public async Task<AuthResponseDto> LoginAsync(
        LoginRequest request,
        ClientContext? client = null,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());

        // AppDbContext exempts ApplicationUser from the SoftDelete filter, so a
        // closed account is still returned here. This is the door that comment
        // refers to: it is closed by refusing to mint a token, not by hiding the row.
        if (user is null || user.IsDeleted)
        {
            BurnPasswordVerification(request.Password);

            await audit.LogAsync(
                "auth.login",
                user?.Id,
                nameof(ApplicationUser),
                user?.Id,
                succeeded: false,
                metadata: new { reason = "invalid_credentials" },
                cancellationToken: cancellationToken);

            throw new BusinessRuleException(InvalidCredentialsMessage, "invalid_credentials");
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            await audit.LogAsync(
                "auth.login",
                user.Id,
                nameof(ApplicationUser),
                user.Id,
                succeeded: false,
                metadata: new { reason = "account_locked" },
                cancellationToken: cancellationToken);

            throw new BusinessRuleException(AccountLockedMessage, "account_locked");
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            // This is the lockoutOnFailure: true half of CheckPasswordSignInAsync.
            await userManager.AccessFailedAsync(user);
            var nowLockedOut = await userManager.IsLockedOutAsync(user);

            await audit.LogAsync(
                "auth.login",
                user.Id,
                nameof(ApplicationUser),
                user.Id,
                succeeded: false,
                metadata: new { reason = nowLockedOut ? "account_locked" : "invalid_credentials" },
                cancellationToken: cancellationToken);

            throw nowLockedOut
                ? new BusinessRuleException(AccountLockedMessage, "account_locked")
                : new BusinessRuleException(InvalidCredentialsMessage, "invalid_credentials");
        }

        user.LastLoginAt = clock.UtcNow;

        // Short-circuits to a no-op when the counter is already zero, which is the
        // common case, so this is one round trip on a normal sign-in and not two.
        await userManager.ResetAccessFailedCountAsync(user);
        await userManager.UpdateAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        var (tokens, _) = IssueTokens(user, roles, familyId: null, client);
        await db.SaveChangesAsync(cancellationToken);

        await audit.LogAsync(
            "auth.login",
            user.Id,
            nameof(ApplicationUser),
            user.Id,
            succeeded: true,
            cancellationToken: cancellationToken);

        return AuthMappings.ToAuthResponse(tokens, AuthMappings.ToProfile(user, roles));
    }

    public async Task<AuthTokensDto> RefreshAsync(
        RefreshRequest request,
        ClientContext? client = null,
        CancellationToken cancellationToken = default)
    {
        var presentedHash = tokenService.HashToken(request.RefreshToken);
        var now = clock.UtcNow;

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        // Tracked, not AsNoTracking: this row is about to be stamped as rotated.
        var presented = await db.RefreshTokens
            .IgnoreQueryFilters([UserOwnershipFilter])
            .FirstOrDefaultAsync(x => x.TokenHash == presentedHash, cancellationToken);

        if (presented is null)
        {
            await audit.LogAsync(
                "auth.refresh",
                userId: null,
                nameof(RefreshToken),
                entityId: null,
                succeeded: false,
                metadata: new { reason = "unknown_token" },
                cancellationToken: cancellationToken);

            throw new BusinessRuleException(SessionEndedMessage, "invalid_refresh_token");
        }

        // A token that was already revoked, or already has a successor, is being
        // presented a second time. Only two things produce that: a copy of it is in
        // someone else's hands, or a chain is being replayed. Revoking just this row
        // would be useless — whoever holds the successor keeps the session — so the
        // whole family goes, which logs out the thief and the victim together and
        // makes the theft visible instead of silent.
        if (presented.IsRevoked || presented.ReplacedByTokenHash is not null)
        {
            await RevokeFamilyAsync(presented.FamilyId, "Reuse detected", now, cancellationToken);

            // Committed before throwing: the revocation is the entire point of this
            // branch, and letting the exception unwind an open transaction would roll
            // it straight back and leave the stolen chain alive.
            await transaction.CommitAsync(cancellationToken);

            logger.LogWarning(
                "Refresh token reuse detected for user {UserId}; family {FamilyId} revoked.",
                presented.UserId,
                presented.FamilyId);

            await audit.LogAsync(
                "auth.refresh_token_reuse_detected",
                presented.UserId,
                nameof(RefreshToken),
                presented.Id,
                succeeded: false,
                metadata: new { familyId = presented.FamilyId },
                cancellationToken: cancellationToken);

            throw new BusinessRuleException(
                "For your security this session was ended. Please sign in again.",
                "refresh_token_reuse");
        }

        if (presented.IsExpiredAt(now))
        {
            throw new BusinessRuleException(
                "Your session has expired. Please sign in again.",
                "refresh_token_expired");
        }

        var user = await userManager.FindByIdAsync(presented.UserId.ToString());
        if (user is null || user.IsDeleted)
        {
            await RevokeFamilyAsync(presented.FamilyId, "Account unavailable", now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            throw new BusinessRuleException(SessionEndedMessage, "invalid_refresh_token");
        }

        var roles = await userManager.GetRolesAsync(user);
        var (tokens, successor) = IssueTokens(user, roles, presented.FamilyId, client);

        presented.RevokedAt = now;
        presented.RevokedReason = "Rotated";
        presented.ReplacedByTokenHash = successor.TokenHash;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await audit.LogAsync(
            "auth.refresh",
            user.Id,
            nameof(RefreshToken),
            presented.Id,
            succeeded: true,
            cancellationToken: cancellationToken);

        return tokens;
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken)) return;

        var presentedHash = tokenService.HashToken(request.RefreshToken);

        var query = db.RefreshTokens
            .AsNoTracking()
            .IgnoreQueryFilters([UserOwnershipFilter])
            .Where(x => x.TokenHash == presentedHash);

        // The client normally calls this with a bearer token, and when it does the
        // row has to belong to that caller. It is still allowed anonymously, because
        // a client whose access token has already expired must be able to hand its
        // refresh token back rather than leave a live family behind.
        if (currentUser.UserId is { } userId)
        {
            query = query.Where(x => x.UserId == userId);
        }

        var match = await query
            .Select(x => new { x.FamilyId, x.UserId })
            .FirstOrDefaultAsync(cancellationToken);

        // Silence on a miss is deliberate. A 404 here would let anyone test values
        // against the token table for free, and a logout that can fail is a logout
        // users learn to skip.
        if (match is null) return;

        await RevokeFamilyAsync(match.FamilyId, "Signed out", clock.UtcNow, cancellationToken);

        await audit.LogAsync(
            "auth.logout",
            match.UserId,
            nameof(RefreshToken),
            entityId: null,
            succeeded: true,
            cancellationToken: cancellationToken);
    }

    public async Task ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());

        // Returning anything different for a known and an unknown address turns this
        // endpoint into a free membership check for any email list someone owns, and
        // it needs no credentials at all to run.
        if (user is null || user.IsDeleted)
        {
            logger.LogInformation("Password reset requested for an address with no active account.");
            return;
        }

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);

        try
        {
            await emailSender.SendPasswordResetAsync(
                user.Email!,
                user.FullName,
                resetToken,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // A send failure must not change the response either: a 500 for real
            // addresses and a 200 for unknown ones is the same oracle by another name.
            logger.LogError(ex, "Password reset email could not be sent for user {UserId}.", user.Id);
        }

        await audit.LogAsync(
            "auth.password_reset_requested",
            user.Id,
            nameof(ApplicationUser),
            user.Id,
            succeeded: true,
            cancellationToken: cancellationToken);
    }

    public async Task ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());

        // Unknown address, closed account and wrong token all answer identically,
        // so completing this form never reveals which of the three it was.
        if (user is null || user.IsDeleted)
        {
            throw new BusinessRuleException(ResetFailedMessage, "invalid_reset_token");
        }

        // Rotates the security stamp as a side effect, so access tokens minted
        // before this moment stop validating as well as the refresh tokens below.
        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);

        if (!result.Succeeded)
        {
            await audit.LogAsync(
                "auth.password_reset",
                user.Id,
                nameof(ApplicationUser),
                user.Id,
                succeeded: false,
                metadata: new { codes = result.Errors.Select(e => e.Code).ToArray() },
                cancellationToken: cancellationToken);

            // A password that fails the policy is worth describing precisely; a bad
            // token is not, because a specific message would confirm the address.
            throw result.Errors.Any(e => e.Code == "InvalidToken")
                ? new BusinessRuleException(ResetFailedMessage, "invalid_reset_token")
                : Translate(result, "weak_password");
        }

        await RevokeAllForUserAsync(user.Id, "Password reset", clock.UtcNow, cancellationToken);

        // Whoever completed this proved control of the mailbox, so leaving them shut
        // out by someone else's failed attempts would hand an attacker a denial of
        // service they can trigger from the login form alone.
        await userManager.ResetAccessFailedCountAsync(user);
        await userManager.SetLockoutEndDateAsync(user, null);

        await audit.LogAsync(
            "auth.password_reset",
            user.Id,
            nameof(ApplicationUser),
            user.Id,
            succeeded: true,
            cancellationToken: cancellationToken);
    }

    public async Task ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.IsDeleted)
        {
            // The principal came from a token this server signed, so an account that
            // has gone since is a missing resource rather than a failed sign-in.
            throw new NotFoundException("Account", userId);
        }

        var result = await userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword);

        if (!result.Succeeded)
        {
            await audit.LogAsync(
                "auth.password_changed",
                userId,
                nameof(ApplicationUser),
                userId,
                succeeded: false,
                metadata: new { codes = result.Errors.Select(e => e.Code).ToArray() },
                cancellationToken: cancellationToken);

            throw result.Errors.Any(e => e.Code == "PasswordMismatch")
                ? new BusinessRuleException("Your current password is not correct.", "invalid_current_password")
                : Translate(result, "weak_password");
        }

        // Every device is signed out, including this one. Sparing the caller's own
        // family would mean identifying it, and the only thing that identifies it is
        // a refresh token this endpoint never receives — guessing from the access
        // token would spare whichever family the request happened to arrive on, which
        // is precisely the wrong one if the reason for the change is a stolen device.
        // Identity has already rotated the security stamp, so the caller's access
        // token stops validating too and the client simply signs in again.
        await RevokeAllForUserAsync(userId, "Password changed", clock.UtcNow, cancellationToken);

        await audit.LogAsync(
            "auth.password_changed",
            userId,
            nameof(ApplicationUser),
            userId,
            succeeded: true,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Mints an access token and a refresh token and stages the refresh row.
    /// Synchronous and deliberately not saving: callers decide whether the row
    /// belongs to their transaction or to a standalone save.
    /// </summary>
    private (AuthTokensDto Tokens, RefreshToken Entity) IssueTokens(
        ApplicationUser user,
        IList<string> roles,
        Guid? familyId,
        ClientContext? client)
    {
        var access = tokenService.CreateAccessToken(user, roles);
        var refresh = tokenService.CreateRefreshToken(
            user.Id,
            familyId,
            client?.IpAddress,
            client?.UserAgent);

        db.RefreshTokens.Add(refresh.Entity);

        var tokens = new AuthTokensDto(
            access.Value,
            refresh.RawValue,
            access.ExpiresAt,
            refresh.Entity.ExpiresAt);

        return (tokens, refresh.Entity);
    }

    /// <summary>Revokes every still-live token descended from one sign-in.</summary>
    private Task<int> RevokeFamilyAsync(
        Guid familyId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        db.RefreshTokens
            .IgnoreQueryFilters([UserOwnershipFilter])
            .Where(x => x.FamilyId == familyId && x.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.RevokedAt, (DateTimeOffset?)now)
                    .SetProperty(x => x.RevokedReason, reason)
                    // ExecuteUpdate goes straight to SQL and never reaches the
                    // SaveChanges interceptor, so the audit stamp is set by hand.
                    .SetProperty(x => x.UpdatedAt, (DateTimeOffset?)now),
                cancellationToken);

    /// <summary>Signs one account out of every device, in a single statement.</summary>
    private Task<int> RevokeAllForUserAsync(
        Guid userId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        db.RefreshTokens
            .IgnoreQueryFilters([UserOwnershipFilter])
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.RevokedAt, (DateTimeOffset?)now)
                    .SetProperty(x => x.RevokedReason, reason)
                    .SetProperty(x => x.UpdatedAt, (DateTimeOffset?)now),
                cancellationToken);

    private static UserSettings BuildSettings(Guid userId, RegisterRequest request)
    {
        var settings = new UserSettings { UserId = userId };

        if (!string.IsNullOrWhiteSpace(request.CurrencyCode))
        {
            // The column is a fixed-width nchar(3); anything else is a client bug and
            // the entity default is a better answer than a truncated code.
            var code = request.CurrencyCode.Trim().ToUpperInvariant();
            if (code.Length == 3) settings.CurrencyCode = code;
        }

        // A hint this server cannot resolve must not be stored: PeriodCalculator
        // falls back to UTC for an unknown zone, so accepting it would silently file
        // every Asia/Kolkata evening transaction under the previous day, forever.
        if (!string.IsNullOrWhiteSpace(request.TimeZoneId)
            && TimeZoneInfo.TryFindSystemTimeZoneById(request.TimeZoneId, out _))
        {
            settings.TimeZoneId = request.TimeZoneId;
        }

        return settings;
    }

    private async Task EnsureUserRoleExistsAsync()
    {
        if (await roleManager.RoleExistsAsync(RoleNames.User)) return;

        // Startup seeding normally owns this. The guard exists because
        // AddToRoleAsync throws a bare InvalidOperationException for a missing role,
        // which would reach a user as an unexplained 500 on the very first request a
        // freshly provisioned environment ever serves.
        var created = await roleManager.CreateAsync(new ApplicationRole(RoleNames.User)
        {
            Description = "Standard account. Sees only its own data.",
        });

        if (!created.Succeeded)
        {
            throw new BusinessRuleException(
                "Registration is temporarily unavailable. Please try again shortly.",
                "role_unavailable");
        }
    }

    /// <summary>
    /// Spends the same PBKDF2 work a real verification would, for a sign-in
    /// attempt that has no user to verify against.
    ///
    /// Without this the endpoint answers a non-existent address in microseconds
    /// and a real one in tens of milliseconds, which enumerates the user table
    /// from timing alone — no failed-login budget spent, nothing in the logs to
    /// distinguish it from ordinary traffic.
    /// </summary>
    private void BurnPasswordVerification(string presentedPassword)
    {
        var decoy = new ApplicationUser { UserName = "decoy" };

        // Hashed by the configured hasher rather than pasted in as a constant, so the
        // decoy always carries the same iteration count as real stored hashes and the
        // two cannot drift apart when PasswordHasherOptions changes.
        _decoyPasswordHash ??= userManager.PasswordHasher.HashPassword(decoy, Guid.NewGuid().ToString("N"));

        _ = userManager.PasswordHasher.VerifyHashedPassword(decoy, _decoyPasswordHash, presentedPassword);
    }

    /// <summary>
    /// Turns an <see cref="IdentityResult"/> into the domain exception the API
    /// middleware knows how to map. Identity's descriptions are already written
    /// for end users, so they are passed through rather than paraphrased.
    /// </summary>
    private static DomainException Translate(IdentityResult result, string errorCode)
    {
        if (result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
        {
            return new ConflictException("An account already exists for that email address.");
        }

        var message = string.Join(" ", result.Errors.Select(e => e.Description));

        return new BusinessRuleException(
            string.IsNullOrWhiteSpace(message) ? "That request could not be completed." : message,
            errorCode);
    }
}
