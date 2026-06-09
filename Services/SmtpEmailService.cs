using System.Net;
using System.Net.Mail;

namespace SyncBook.Server.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendVerificationCodeEmailAsync(
        string toEmail,
        string code,
        VerificationEmailPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        var (subject, heading, detail) = purpose switch
        {
            VerificationEmailPurpose.Registration => (
                "SyncBook — Verify your email",
                "Verify your email address",
                "Enter this code to continue creating your SyncBook account:"),
            VerificationEmailPurpose.PasswordReset => (
                "SyncBook — Password reset code",
                "Reset your password",
                "Enter this code on the reset password page to choose a new password:"),
            _ => throw new ArgumentOutOfRangeException(nameof(purpose))
        };

        var fromAddress = _configuration["Smtp:From"] ?? string.Empty;
        using var client = CreateClient();
        if (client is null || string.IsNullOrWhiteSpace(fromAddress))
        {
            _logger.LogWarning(
                "SMTP is not configured. {Purpose} code for {Email}: {Code}",
                purpose,
                toEmail,
                code);
            return;
        }

        var htmlBody = $"""
            <div style="font-family:Inter,Arial,sans-serif;max-width:480px;margin:0 auto;padding:24px;">
              <h2 style="color:#0f766e;margin:0 0 12px;">{heading}</h2>
              <p style="color:#475569;margin:0 0 20px;">{detail}</p>
              <div style="background:#f0fdf4;border:1px solid #10b981;border-radius:12px;padding:20px;text-align:center;">
                <span style="font-size:32px;font-weight:700;letter-spacing:8px;color:#0f766e;">{code}</span>
              </div>
              <p style="color:#94a3b8;font-size:13px;margin:20px 0 0;">This code expires in 15 minutes. If you did not request this, you can ignore this email.</p>
            </div>
            """;

        using var message = new MailMessage(fromAddress, toEmail)
        {
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };

        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            $"{heading}\n\n{detail}\n\n{code}\n\nThis code expires in 15 minutes.",
            null,
            "text/plain"));

        await client.SendMailAsync(message, cancellationToken);
    }

    public async Task SendPasswordResetLinkEmailAsync(
        string toEmail,
        string resetUrl,
        bool isInitialSetup,
        CancellationToken cancellationToken = default)
    {
        var (subject, heading, detail, buttonLabel) = isInitialSetup
            ? (
                "SyncBook — Set your password",
                "Set your password",
                "Click the button below to create a password so you can also sign in with email and password:",
                "Set password")
            : (
                "SyncBook — Reset your password",
                "Reset your password",
                "Click the button below to choose a new password for your SyncBook account:",
                "Reset password");

        var fromAddress = _configuration["Smtp:From"] ?? string.Empty;
        using var client = CreateClient();
        if (client is null || string.IsNullOrWhiteSpace(fromAddress))
        {
            _logger.LogWarning(
                "SMTP is not configured. Password reset link for {Email}: {ResetUrl}",
                toEmail,
                resetUrl);
            return;
        }

        var htmlBody = $"""
            <div style="font-family:Inter,Arial,sans-serif;max-width:480px;margin:0 auto;padding:24px;">
              <h2 style="color:#0f766e;margin:0 0 12px;">{heading}</h2>
              <p style="color:#475569;margin:0 0 20px;">{detail}</p>
              <div style="text-align:center;margin:24px 0;">
                <a href="{resetUrl}" style="display:inline-block;background:#0f766e;color:#ffffff;text-decoration:none;padding:14px 28px;border-radius:8px;font-weight:600;">{buttonLabel}</a>
              </div>
              <p style="color:#94a3b8;font-size:13px;margin:20px 0 0;">This link expires in 15 minutes. If you did not request this, you can ignore this email.</p>
              <p style="color:#94a3b8;font-size:12px;margin:12px 0 0;word-break:break-all;">{resetUrl}</p>
            </div>
            """;

        using var message = new MailMessage(fromAddress, toEmail)
        {
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };

        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            $"{heading}\n\n{detail}\n\n{resetUrl}\n\nThis link expires in 15 minutes.",
            null,
            "text/plain"));

        await client.SendMailAsync(message, cancellationToken);
    }

    private SmtpClient? CreateClient()
    {
        var fromAddress = _configuration["Smtp:From"] ?? string.Empty;
        var host = _configuration["Smtp:Host"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromAddress))
        {
            return null;
        }

        var port = int.TryParse(_configuration["Smtp:Port"], out var smtpPort) ? smtpPort : 587;
        var user = _configuration["Smtp:User"];
        var password = _configuration["Smtp:Password"];

        return new SmtpClient(host, port)
        {
            EnableSsl = true,
            Credentials = string.IsNullOrWhiteSpace(user)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(user, password)
        };
    }
}
