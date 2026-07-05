using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.DTOs.Auth;
using TripNest.Core.Enums;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Coverage for anonymous phone-OTP login (POST /api/auth/phone-login/send-otp + verify-otp).
/// Uses the recording SMS sender to capture the real code. Asserts the enumeration-safety
/// contract: unknown and ambiguous (shared) numbers get the same generic response and no SMS.
/// </summary>
public class PhoneLoginTests : TestBase
{
    private static string UniquePhone() => $"+23350{Random.Shared.Next(1_000_000, 9_999_999)}";

    private RecordingSmsSender Sms => _fixture.Services.GetRequiredService<RecordingSmsSender>();

    private Task<HttpResponseMessage> SendOtpAsync(string phone) =>
        _httpClient.PostAsJsonAsync("/api/auth/phone-login/send-otp", new PhoneLoginStartRequest { Phone = phone });

    private Task<HttpResponseMessage> VerifyOtpAsync(string phone, string code) =>
        _httpClient.PostAsJsonAsync("/api/auth/phone-login/verify-otp", new PhoneLoginVerifyRequest { Phone = phone, Code = code });

    private string CapturedCodeFor(string phone)
    {
        var (_, message) = Assert.Single(Sms.Sent, s => s.Phone == phone);
        return Regex.Match(message, @"\d{6}").Value;
    }

    [Fact]
    public async Task PhoneLogin_RegisteredPhone_SendsCodeAndSignsIn()
    {
        var phone = UniquePhone();
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant, phone: phone);
        ClearAuth();

        var sendResponse = await SendOtpAsync(phone);
        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);
        var code = CapturedCodeFor(phone);
        Assert.Matches(@"^\d{6}$", code);

        var verifyResponse = await VerifyOtpAsync(phone, code);
        var body = await verifyResponse.Content.ReadAsStringAsync();
        Assert.True(verifyResponse.StatusCode == HttpStatusCode.OK, $"Expected OK but got {verifyResponse.StatusCode}: {body}");

        var data = JsonDocument.Parse(body).RootElement.GetProperty("data");
        Assert.Equal(userId, data.GetProperty("userId").GetString());
        Assert.True(data.GetProperty("phoneVerified").GetBoolean());

        // The issued access token must work against a protected endpoint.
        var token = data.GetProperty("accessToken").GetString()!;
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var authed = await _httpClient.GetAsync("/api/escrow/mine");
        Assert.Equal(HttpStatusCode.OK, authed.StatusCode);
    }

    [Fact]
    public async Task PhoneLogin_UnknownPhone_SameGenericResponse_NoSms()
    {
        var phone = UniquePhone();

        var response = await SendOtpAsync(phone);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(Sms.Sent, s => s.Phone == phone);
    }

    [Fact]
    public async Task PhoneLogin_AmbiguousPhone_SameGenericResponse_NoSms()
    {
        // Phone is not unique in the schema; a number shared by two accounts must not sign in
        // (and must not be distinguishable from an unknown number).
        var phone = UniquePhone();
        await RegisterAndLoginAsync(UserRole.Tenant, phone: phone);
        await RegisterAndLoginAsync(UserRole.Tenant, phone: phone);
        ClearAuth();

        var response = await SendOtpAsync(phone);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(Sms.Sent, s => s.Phone == phone);
    }

    [Fact]
    public async Task PhoneLogin_WrongCode_IsRejected_ThenCorrectCodeStillWorks()
    {
        var phone = UniquePhone();
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant, phone: phone);
        ClearAuth();

        await SendOtpAsync(phone);
        var code = CapturedCodeFor(phone);
        var wrongCode = code == "000000" ? "111111" : "000000";

        var wrong = await VerifyOtpAsync(phone, wrongCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var right = await VerifyOtpAsync(phone, code);
        var body = await right.Content.ReadAsStringAsync();
        Assert.True(right.StatusCode == HttpStatusCode.OK, $"Expected OK but got {right.StatusCode}: {body}");
        Assert.Equal(userId, JsonDocument.Parse(body).RootElement
            .GetProperty("data").GetProperty("userId").GetString());
    }

    [Fact]
    public async Task PhoneLogin_CodeIsSingleUse()
    {
        var phone = UniquePhone();
        await RegisterAndLoginAsync(UserRole.Tenant, phone: phone);
        ClearAuth();

        await SendOtpAsync(phone);
        var code = CapturedCodeFor(phone);

        var first = await VerifyOtpAsync(phone, code);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Replaying the consumed code must fail.
        var replay = await VerifyOtpAsync(phone, code);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }
}
