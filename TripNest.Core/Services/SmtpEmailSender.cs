using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// SMTP email sender (e.g. Gmail) using MailKit, with graceful fallback: if SmtpSettings aren't
/// configured, it logs the email and returns false instead of throwing, so notification
/// side-effects never break the underlying business action.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly string? _host;
    private readonly int _port;
    private readonly string? _username;
    private readonly string? _password;
    private readonly bool _useStartTls;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _logger = logger;
        _host = configuration["SmtpSettings:Host"];
        _port = int.TryParse(configuration["SmtpSettings:Port"], out var port) ? port : 587;
        _username = configuration["SmtpSettings:Username"];
        _password = configuration["SmtpSettings:Password"];
        // Gmail uses STARTTLS on 587; flip to false to use implicit SSL on 465.
        _useStartTls = !bool.TryParse(configuration["SmtpSettings:UseStartTls"], out var startTls) || startTls;
        // Gmail requires the From address to match the authenticated account.
        _fromEmail = configuration["SmtpSettings:FromEmail"] ?? _username ?? "noreply@tripnest.app";
        _fromName = configuration["SmtpSettings:FromName"] ?? "TripNest";
    }

    public async Task<bool> SendAsync(string toEmail, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(_host) || string.IsNullOrWhiteSpace(_username) || string.IsNullOrWhiteSpace(_password))
        {
            _logger.LogInformation("[Email not configured] would send to {ToEmail}: {Subject}", toEmail, subject);
            return false;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_fromName, _fromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient();
            var secureOption = _useStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect;
            await client.ConnectAsync(_host, _port, secureOption);
            await client.AuthenticateAsync(_username, _password);
            await client.SendAsync(message);
            await client.DisconnectAsync(quit: true);

            _logger.LogInformation("Email sent to {ToEmail} via SMTP", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail}", toEmail);
            return false;
        }
    }
}
