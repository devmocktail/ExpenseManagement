using ExpenseManagement.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExpenseManagement.Infrastructure.Services;

/// <summary>
/// Stand-in for a real transactional email provider: it writes a structured log
/// line and sends nothing.
///
/// Nothing leaves the machine, so a developer running the password-reset flow
/// against a copy of production data cannot mail a stranger by accident. Every
/// line is emitted with named placeholders rather than an interpolated string so
/// the recipient stays a queryable field when these logs reach a structured
/// sink.
/// </summary>
public sealed class DevelopmentEmailSender(
    IHostEnvironment environment,
    ILogger<DevelopmentEmailSender> logger) : IEmailSender
{
    /// <summary>
    /// Captured once at construction: the answer cannot change during the
    /// process lifetime, and reading it per call would invite someone to make it
    /// configurable later — which is the one thing this flag must never be.
    /// </summary>
    private readonly bool _isDevelopment = environment.IsDevelopment();

    public Task SendPasswordResetAsync(
        string toEmail,
        string displayName,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Password reset email suppressed (development sender). Recipient {Recipient} ({DisplayName}) requested a reset.",
            toEmail,
            displayName);

        if (_isDevelopment)
        {
            // Development only, and this guard is the reason the class is safe to
            // register at all. A reset token is a bearer credential for the
            // account: anyone who can read the log — a shipped aggregator, a
            // support bundle, a CI artefact, a screen-shared terminal — could
            // complete the reset themselves without ever touching the mailbox.
            // Outside Development the token stays in the response path only.
            logger.LogWarning(
                "Development-only: password reset token for {Recipient} is {ResetToken}.",
                toEmail,
                resetToken);
        }

        return Task.CompletedTask;
    }

    public Task SendWelcomeAsync(
        string toEmail,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Welcome email suppressed (development sender). Recipient {Recipient} ({DisplayName}).",
            toEmail,
            displayName);

        return Task.CompletedTask;
    }
}
