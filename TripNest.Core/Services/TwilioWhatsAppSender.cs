using TripNest.Core.Interfaces.Services;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace TripNest.Core.Services;

/// <summary>
/// Sends WhatsApp messages via Twilio (same account as SMS; addresses use the whatsapp: prefix).
/// Needs TwilioSettings:WhatsAppFromNumber (e.g. the Twilio sandbox number) in addition to the
/// account SID/token. Graceful fallback: logs + returns false when not configured.
/// </summary>
public class TwilioWhatsAppSender : IWhatsAppSender
{
    private readonly ILogger<TwilioWhatsAppSender> _logger;
    private readonly string? _accountSid;
    private readonly string? _authToken;
    private readonly string? _fromNumber;

    public TwilioWhatsAppSender(IConfiguration configuration, ILogger<TwilioWhatsAppSender> logger)
    {
        _logger = logger;
        _accountSid = configuration["TwilioSettings:AccountSid"];
        _authToken = configuration["TwilioSettings:AuthToken"];
        _fromNumber = configuration["TwilioSettings:WhatsAppFromNumber"];
    }

    public async Task<bool> SendAsync(string phoneNumber, string message)
    {
        if (string.IsNullOrWhiteSpace(_accountSid) || string.IsNullOrWhiteSpace(_authToken) || string.IsNullOrWhiteSpace(_fromNumber))
        {
            _logger.LogInformation("[WhatsApp not configured] would send to {PhoneNumber}: {Message}", phoneNumber, message);
            return false;
        }

        try
        {
            TwilioClient.Init(_accountSid, _authToken);
            var result = await MessageResource.CreateAsync(
                to: new PhoneNumber($"whatsapp:{phoneNumber}"),
                from: new PhoneNumber($"whatsapp:{_fromNumber}"),
                body: message);
            _logger.LogInformation("WhatsApp sent to {PhoneNumber} (sid {Sid})", phoneNumber, result.Sid);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WhatsApp to {PhoneNumber}", phoneNumber);
            return false;
        }
    }
}
