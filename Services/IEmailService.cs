namespace SyncBook.Server.Services;

public interface IEmailService
{
    Task SendVerificationCodeEmailAsync(
        string toEmail,
        string code,
        VerificationEmailPurpose purpose,
        CancellationToken cancellationToken = default);
}

public enum VerificationEmailPurpose
{
    Registration,
    PasswordReset
}
