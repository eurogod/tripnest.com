using Microsoft.Extensions.Configuration;
using TripNest.Core.Services;
using Xunit.Abstractions;

namespace TripNest.Core.Tests.Live;

/// <summary>
/// Opt-in tests that hit the REAL Twilio / SendGrid / Paystack using the keys in the Core
/// project's user-secrets. They are skipped (no-op) unless RUN_LIVE_INTEGRATION=1, so the
/// normal suite and CI stay green and your phones aren't spammed on every run.
///
/// Run them with:  RUN_LIVE_INTEGRATION=1 dotnet test --filter "FullyQualifiedName~Live"
///
/// Paystack is asserted (test mode always works). SMS/WhatsApp/email outcomes are logged per
/// recipient rather than asserted, because Twilio trial only delivers to verified numbers, the
/// WhatsApp sandbox needs a join, and SendGrid needs a verified sender — so a "FAILED" line is
/// an actionable signal, not a broken test.
/// </summary>
public class LiveIntegrationTests
{
    private readonly ITestOutputHelper _out;
    private readonly IConfiguration _config;

    private static bool Enabled => Environment.GetEnvironmentVariable("RUN_LIVE_INTEGRATION") == "1";

    // Recipients come from config (user-secrets/env), never hardcoded, so no PII lands in the repo.
    //   dotnet user-secrets set "LiveTest:Phones" "0xxxxxxxxx,0yyyyyyyyy" --project TripNest.Core
    //   dotnet user-secrets set "LiveTest:Emails" "a@example.com,b@example.com" --project TripNest.Core
    private string[] Phones => Split(_config["LiveTest:Phones"]);
    private string[] Emails => Split(_config["LiveTest:Emails"]);

    private static string[] Split(string? csv) =>
        (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public LiveIntegrationTests(ITestOutputHelper output)
    {
        _out = output;
        // Load the Core project's user-secrets (where the real keys + test recipients live).
        _config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(TwilioSmsSender).Assembly)
            .AddEnvironmentVariables()
            .Build();
    }

    [Fact]
    public async Task Paystack_Initialize_Live()
    {
        if (!Enabled) { _out.WriteLine("SKIPPED — set RUN_LIVE_INTEGRATION=1 to run."); return; }

        var gateway = new PaystackPaymentGateway(new HttpClient(), _config, new XunitLogger<PaystackPaymentGateway>(_out));
        var result = await gateway.InitiatePaymentAsync(50m, "GHS", Emails[0], "live-test-booking");

        _out.WriteLine($"Paystack initialize: success={result.Success}, ref={result.Reference}");
        _out.WriteLine($"  checkout: {result.CheckoutUrl}");
        Assert.True(result.Success, "Paystack initialize should succeed with the test key");
    }

    [Fact]
    public async Task Sms_AllNumbers_Live()
    {
        if (!Enabled) { _out.WriteLine("SKIPPED — set RUN_LIVE_INTEGRATION=1 to run."); return; }
        if (Phones.Length == 0) { _out.WriteLine("No recipients — set LiveTest:Phones in user-secrets."); return; }

        var sms = new TwilioSmsSender(_config, new XunitLogger<TwilioSmsSender>(_out));
        var validator = new PhoneNumberValidator(_config);

        foreach (var phone in Phones)
        {
            var e164 = validator.Normalize(phone) ?? phone;
            var ok = await sms.SendSmsAsync(e164, "TripNest: your SMS notifications are working ✅");
            _out.WriteLine($"SMS  {phone} ({e164}): {(ok ? "SENT ✅" : "FAILED ❌")}");
        }
    }

    [Fact]
    public async Task WhatsApp_FirstNumber_Live()
    {
        if (!Enabled) { _out.WriteLine("SKIPPED — set RUN_LIVE_INTEGRATION=1 to run."); return; }
        if (Phones.Length == 0) { _out.WriteLine("No recipients — set LiveTest:Phones in user-secrets."); return; }

        var whatsApp = new TwilioWhatsAppSender(_config, new XunitLogger<TwilioWhatsAppSender>(_out));
        var validator = new PhoneNumberValidator(_config);
        var e164 = validator.Normalize(Phones[0]) ?? Phones[0];

        var ok = await whatsApp.SendAsync(e164, "TripNest: WhatsApp notifications are working ✅");
        _out.WriteLine($"WhatsApp {Phones[0]} ({e164}): {(ok ? "SENT ✅" : "FAILED ❌ (recipient must join the sandbox first)")}");
    }

    [Fact]
    public async Task Email_BothAddresses_Live()
    {
        if (!Enabled) { _out.WriteLine("SKIPPED — set RUN_LIVE_INTEGRATION=1 to run."); return; }
        if (Emails.Length == 0) { _out.WriteLine("No recipients — set LiveTest:Emails in user-secrets."); return; }

        var email = new SendGridEmailSender(_config, new XunitLogger<SendGridEmailSender>(_out));
        foreach (var addr in Emails)
        {
            var ok = await email.SendAsync(addr, "TripNest test email",
                "<p>Your TripNest email notifications are working ✅</p>");
            _out.WriteLine($"Email {addr}: {(ok ? "SENT ✅" : "FAILED ❌ (needs a verified SendGrid sender / FromEmail)")}");
        }
    }
}
