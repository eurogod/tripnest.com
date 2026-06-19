using SendGrid;
using SendGrid.Helpers.Mail;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Real SendGrid email sender with graceful fallback: if SendGridSettings aren't configured,
/// it logs the email and returns false instead of throwing, so notification side-effects never
/// break the underlying business action.
/// </summary>
public class SendGridEmailSender : IEmailSender
{
    private readonly ILogger<SendGridEmailSender> _logger;
    private readonly string? _apiKey;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public SendGridEmailSender(IConfiguration configuration, ILogger<SendGridEmailSender> logger)
    {
        _logger = logger;
        _apiKey = configuration["SendGridSettings:ApiKey"];
        _fromEmail = configuration["SendGridSettings:FromEmail"] ?? "noreply@tripnest.app";
        _fromName = configuration["SendGridSettings:FromName"] ?? "TripNest";
    }

    public async Task<bool> SendAsync(string toEmail, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogInformation("[Email not configured] would send to {ToEmail}: {Subject}", toEmail, subject);
            return false;
        }

        try
        {
            var client = new SendGridClient(_apiKey);
            var msg = MailHelper.CreateSingleEmail(
                new EmailAddress(_fromEmail, _fromName),
                new EmailAddress(toEmail),
                subject,
                plainTextContent: null,
                htmlContent: htmlBody);
            var response = await client.SendEmailAsync(msg);

            if ((int)response.StatusCode is >= 200 and < 300)
            {
                _logger.LogInformation("Email sent to {ToEmail} ({Status})", toEmail, response.StatusCode);
                return true;
            }

            _logger.LogWarning("SendGrid returned {Status} sending to {ToEmail}", response.StatusCode, toEmail);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail}", toEmail);
            return false;
        }
    }
}
