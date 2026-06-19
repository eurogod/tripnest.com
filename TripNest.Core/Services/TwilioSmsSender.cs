using TripNest.Core.Interfaces.Services;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace TripNest.Core.Services;

/// <summary>
/// Real Twilio SMS sender with graceful fallback: if TwilioSettings aren't configured
/// (e.g. local dev / CI without keys), it logs the message and returns false instead of
/// throwing, so notification side-effects never break the underlying business action.
/// </summary>
public class TwilioSmsSender : ISmsSender
{
    private readonly ILogger<TwilioSmsSender> _logger;
    private readonly string? _accountSid;
    private readonly string? _authToken;
    private readonly string? _fromNumber;

    public TwilioSmsSender(IConfiguration configuration, ILogger<TwilioSmsSender> logger)
    {
        _logger = logger;
        _accountSid = configuration["TwilioSettings:AccountSid"];
        _authToken = configuration["TwilioSettings:AuthToken"];
        _fromNumber = configuration["TwilioSettings:FromPhoneNumber"];
    }

    public async Task<bool> SendSmsAsync(string phoneNumber, string message)
    {
        if (string.IsNullOrWhiteSpace(_accountSid) || string.IsNullOrWhiteSpace(_authToken) || string.IsNullOrWhiteSpace(_fromNumber))
        {
            _logger.LogInformation("[SMS not configured] would send to {PhoneNumber}: {Message}", phoneNumber, message);
            return false;
        }

        try
        {
            TwilioClient.Init(_accountSid, _authToken);
            var result = await MessageResource.CreateAsync(
                to: new PhoneNumber(phoneNumber),
                from: new PhoneNumber(_fromNumber),
                body: message);
            _logger.LogInformation("SMS sent to {PhoneNumber} (sid {Sid})", phoneNumber, result.Sid);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {PhoneNumber}", phoneNumber);
            return false;
        }
    }
}
