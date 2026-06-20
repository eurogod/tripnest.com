using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Enums;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Phone-ownership OTP: requesting a code sends it over SMS (captured by the recording sender),
/// and submitting the correct code verifies the phone while a wrong code is rejected.
/// </summary>
public class PhoneVerificationTests : TestBase
{
    private RecordingSmsSender Sms => _fixture.Services.GetRequiredService<RecordingSmsSender>();

    [Fact]
    public async Task SendThenVerify_WithCorrectCode_VerifiesPhone()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);
        Sms.Sent.Clear();

        var send = await _httpClient.PostAsJsonAsync("/api/auth/phone/send-otp", new { channel = "sms" });
        Assert.Equal(HttpStatusCode.OK, send.StatusCode);

        // Recover the code from the SMS the recording sender captured.
        var message = Assert.Single(Sms.Sent).Message;
        var code = Regex.Match(message, @"\d{6}").Value;
        Assert.NotEqual(string.Empty, code);

        var verify = await _httpClient.PostAsJsonAsync("/api/auth/phone/verify-otp", new { code });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
    }

    [Fact]
    public async Task SendOtp_TwiceInQuickSuccession_SecondIsThrottled()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);
        Sms.Sent.Clear();

        var first = await _httpClient.PostAsJsonAsync("/api/auth/phone/send-otp", new { channel = "sms" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _httpClient.PostAsJsonAsync("/api/auth/phone/send-otp", new { channel = "sms" });
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);

        // Only the first send actually dispatched an SMS.
        Assert.Single(Sms.Sent);
    }

    [Fact]
    public async Task Verify_WithWrongCode_ReturnsBadRequest()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);
        await _httpClient.PostAsJsonAsync("/api/auth/phone/send-otp", new { channel = "sms" });

        var verify = await _httpClient.PostAsJsonAsync("/api/auth/phone/verify-otp", new { code = "000000" });
        // Incorrect-code 400 (unless 000000 happened to match the real code — vanishingly unlikely).
        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
    }
}
