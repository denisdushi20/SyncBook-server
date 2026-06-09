namespace SyncBook.Server.Services;

public interface IEmailService
{
    Task SendVerificationCodeEmailAsync(
        string toEmail,
        string code,
        VerificationEmailPurpose purpose,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetLinkEmailAsync(
        string toEmail,
        string resetUrl,
        bool isInitialSetup,
        CancellationToken cancellationToken = default);
}

public enum VerificationEmailPurpose
{
    Registration,
    PasswordReset
}
