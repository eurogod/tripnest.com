using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Enums;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Email-ownership OTP: requesting a code emails it (captured by the recording email sender),
/// submitting the correct code verifies the email, a wrong code is rejected, and a second send
/// within the cooldown window is throttled. Mirrors <see cref="PhoneVerificationTests"/>.
/// </summary>
public class EmailVerificationTests : TestBase
{
    private RecordingEmailSender Email => _fixture.Services.GetRequiredService<RecordingEmailSender>();

    [Fact]
    public async Task SendThenVerify_WithCorrectCode_VerifiesEmail()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);
        Email.Sent.Clear();

        var send = await _httpClient.PostAsJsonAsync("/api/auth/email/send-otp", new { });
        Assert.Equal(HttpStatusCode.OK, send.StatusCode);

        // Recover the code from the email body the recording sender captured.
        var body = Assert.Single(Email.Sent).Body;
        var code = Regex.Match(body, @"\d{6}").Value;
        Assert.NotEqual(string.Empty, code);

        var verify = await _httpClient.PostAsJsonAsync("/api/auth/email/verify-otp", new { code });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
    }

    [Fact]
    public async Task SendOtp_TwiceInQuickSuccession_SecondIsThrottled()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);
        Email.Sent.Clear();

        var first = await _httpClient.PostAsJsonAsync("/api/auth/email/send-otp", new { });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _httpClient.PostAsJsonAsync("/api/auth/email/send-otp", new { });
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);

        Assert.Single(Email.Sent);
    }

    [Fact]
    public async Task Verify_WithWrongCode_ReturnsBadRequest()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);
        await _httpClient.PostAsJsonAsync("/api/auth/email/send-otp", new { });

        var verify = await _httpClient.PostAsJsonAsync("/api/auth/email/verify-otp", new { code = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
    }
}
