using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Resend;

namespace Nobetci.Web.Services;

public interface IEmailSender
{
    Task SendEmailAsync(string email, string subject, string htmlMessage);
}

public class EmailService : IEmailSender
{
    private readonly IResend _resend;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IResend resend,
        IConfiguration configuration,
        ILogger<EmailService> logger)
    {
        _resend = resend;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        try
        {
            var fromEmail = _configuration["Email:FromEmail"] ?? "onboarding@resend.dev";
            var fromName = _configuration["Email:FromName"] ?? "Geldimmi";
            
            // Check if domain is verified (if FromEmail is not onboarding@resend.dev, domain is likely verified)
            var isDomainVerified = !fromEmail.Contains("@resend.dev", StringComparison.OrdinalIgnoreCase);

            // Send email directly to the recipient (domain is treated as verified)
            var message = new EmailMessage
            {
                From = $"{fromName} <{fromEmail}>",
                Subject = subject,
                HtmlBody = htmlMessage
            };
            message.To.Add(email);

            await _resend.EmailSendAsync(message);
            _logger.LogInformation("Email sent successfully via Resend to {Email}. Subject: {Subject} (Domain Verified: {IsVerified})", 
                email, subject, isDomainVerified);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via Resend to {Email}. Subject: {Subject}", email, subject);
            
            // Provide helpful error messages
            if (ex is Resend.ResendException resendEx)
            {
                var fromEmail = _configuration["Email:FromEmail"] ?? "onboarding@resend.dev";
                var isDomainVerified = !fromEmail.Contains("@resend.dev", StringComparison.OrdinalIgnoreCase);
                
                if (!isDomainVerified)
                {
                    _logger.LogWarning(
                        "Resend Error: {Message}\n" +
                        "Domain not verified. To send emails to all recipients:\n" +
                        "1. Go to https://resend.com/domains\n" +
                        "2. Verify your domain (e.g., geldimmi.com)\n" +
                        "3. Update appsettings.json: \"FromEmail\": \"noreply@geldimmi.com\"\n" +
                        "4. Restart the application",
                        resendEx.Message);
                }
                else
                {
                    _logger.LogWarning(
                        "Resend Error: {Message}\n" +
                        "Domain appears to be verified. Check Resend dashboard for details.",
                        resendEx.Message);
                }
            }
            
            // Don't throw - we don't want email failures to break registration
        }
    }
}
