namespace ExpenseManagement.Application.Common.Interfaces;

/// <summary>
/// Transactional email. The development implementation writes to the log
/// instead of sending, so password-reset flows are testable without an SMTP
/// account and no real address is ever contacted from a dev machine.
/// </summary>
public interface IEmailSender
{
    Task SendPasswordResetAsync(string toEmail, string displayName, string resetToken, CancellationToken cancellationToken = default);

    Task SendWelcomeAsync(string toEmail, string displayName, CancellationToken cancellationToken = default);
}
