using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.DTOs.Properties;
using TripNest.Core.Enums;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Coverage for profile signature images (first upload free; edits gated by password + Ghana Card
/// re-auth and a 30-day cooldown) and agreement integrity (terms hash binding, signature-image
/// snapshots, tamper detection between signatures).
/// </summary>
public class SignatureAndAgreementIntegrityTests : TestBase
{
    private const string Password = "Password@123";

    // Minimal valid PNG (1x1 transparent pixel) — storage validates the type by content/extension.
    private static readonly byte[] PngPixel = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static MultipartFormDataContent SignatureForm(string? password = null, string? ghanaCardNumber = null)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(PngPixel);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(file, "signature", "signature.png");
        if (password is not null) content.Add(new StringContent(password), "password");
        if (ghanaCardNumber is not null) content.Add(new StringContent(ghanaCardNumber), "ghanaCardNumber");
        return content;
    }

    [Fact]
    public async Task Signature_FirstUploadFree_EditGatedByCooldownAndPassword()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);

        // No signature yet.
        var info = await DataOf(await _httpClient.GetAsync("/api/profile/signature"));
        Assert.False(info.GetProperty("hasSignature").GetBoolean());

        // First upload needs nothing but the image.
        var first = await _httpClient.PostAsync("/api/profile/signature", SignatureForm());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // An immediate edit is blocked by the 30-day cooldown, regardless of credentials.
        var tooSoon = await _httpClient.PostAsync("/api/profile/signature", SignatureForm(Password));
        Assert.Equal(HttpStatusCode.BadRequest, tooSoon.StatusCode);

        // Age the last change past the cooldown.
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FindAsync(userId);
            user!.SignatureUpdatedAt = DateTime.UtcNow.AddDays(-31);
            await db.SaveChangesAsync();
        }

        // Still refused without (or with a wrong) password…
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _httpClient.PostAsync("/api/profile/signature", SignatureForm())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _httpClient.PostAsync("/api/profile/signature", SignatureForm("WrongPass@123"))).StatusCode);

        // …and accepted with the correct one (account is not identity-verified, so no card needed).
        var edited = await _httpClient.PostAsync("/api/profile/signature", SignatureForm(Password));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
    }

    [Fact]
    public async Task Signature_VerifiedUserEdit_RequiresMatchingGhanaCard()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        Assert.Equal(HttpStatusCode.OK,
            (await _httpClient.PostAsync("/api/profile/signature", SignatureForm())).StatusCode);

        // Verified identity with a card on file; cooldown already elapsed.
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FindAsync(userId);
            user!.IsVerified = true;
            user.SignatureUpdatedAt = DateTime.UtcNow.AddDays(-31);
            db.VerificationRequests.Add(new Models.VerificationRequest
            {
                UserId = userId,
                GhanaCardNumber = "GHA-123456789-0",
                SelfiePhotoPath = "/tmp/selfie.jpg",
                NiaPhotoUrl = string.Empty,
                ClaimedFirstName = "Test",
                ClaimedLastName = "User",
                ClaimedDateOfBirth = new DateOnly(1990, 1, 1),
                Status = VerificationStatus.Verified
            });
            await db.SaveChangesAsync();
        }

        // Password alone is no longer enough…
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _httpClient.PostAsync("/api/profile/signature", SignatureForm(Password))).StatusCode);
        // …a wrong card is refused…
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _httpClient.PostAsync("/api/profile/signature", SignatureForm(Password, "GHA-000000000-9"))).StatusCode);
        // …the card on the verified identity (spacing-insensitive) unlocks the edit.
        var ok = await _httpClient.PostAsync("/api/profile/signature", SignatureForm(Password, "gha-123456789-0"));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Agreement_SnapshotsSignatureImages_BindsTermsHash_AndDetectsTampering()
    {
        var (agreementId, landlordToken) = await CreateAgreementWithSignedTenantAsync();

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var agreement = await db.Agreements.FindAsync(agreementId);
            // The tenant's click bound the terms hash and snapshotted their signature image.
            Assert.False(string.IsNullOrEmpty(agreement!.TermsHash));
            Assert.False(string.IsNullOrEmpty(agreement.TenantSignatureImagePath));

            // Tamper with the terms between the two signatures.
            agreement.TermsContent += "\nThe tenant also agrees to pay double.";
            await db.SaveChangesAsync();
        }

        // The landlord's signature refuses to bind to altered terms (409 — the document
        // conflicts with what the first party signed).
        UseToken(landlordToken);
        var sign = await _httpClient.PostAsync($"/api/agreements/{agreementId}/sign", null);
        Assert.Equal(HttpStatusCode.Conflict, sign.StatusCode);
    }

    [Fact]
    public async Task Agreement_FullySigned_PdfDownloads()
    {
        var (agreementId, landlordToken) = await CreateAgreementWithSignedTenantAsync();

        UseToken(landlordToken);
        Assert.Equal(HttpStatusCode.OK,
            (await _httpClient.PostAsync($"/api/agreements/{agreementId}/sign", null)).StatusCode);

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var agreement = await db.Agreements.FindAsync(agreementId);
            Assert.Equal(AgreementStatus.Signed, agreement!.Status);
        }

        var pdf = await _httpClient.GetAsync($"/api/agreements/{agreementId}/download");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        Assert.True((await pdf.Content.ReadAsByteArrayAsync()).Length > 1000);
    }

    [Fact]
    public async Task Agreement_TerminateAndLazyExpiry_Lifecycle()
    {
        var (agreementId, landlordToken) = await CreateAgreementWithSignedTenantAsync();

        // Draft/Pending agreements can't be terminated.
        var early = await _httpClient.PostAsJsonAsync($"/api/agreements/{agreementId}/terminate", new { reason = "changed my mind" });
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);

        // Landlord co-signs -> Signed; now the tenant terminates with a reason.
        UseToken(landlordToken);
        Assert.Equal(HttpStatusCode.OK, (await _httpClient.PostAsync($"/api/agreements/{agreementId}/sign", null)).StatusCode);
        var terminated = await DataOf(await _httpClient.PostAsJsonAsync(
            $"/api/agreements/{agreementId}/terminate", new { reason = "Tenancy ended early by mutual consent" }));
        Assert.Equal((int)AgreementStatus.Terminated, terminated.GetProperty("status").GetInt32());

        // Lazy expiry: a signed agreement whose stay ended flips to Expired on the next read.
        var (agreement2, landlord2) = await CreateAgreementWithSignedTenantAsync();
        UseToken(landlord2);
        Assert.Equal(HttpStatusCode.OK, (await _httpClient.PostAsync($"/api/agreements/{agreement2}/sign", null)).StatusCode);
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var a = await db.Agreements.FindAsync(agreement2);
            a!.ExpiryDate = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }
        var read = await DataOf(await _httpClient.GetAsync($"/api/agreements/{agreement2}"));
        Assert.Equal((int)AgreementStatus.Expired, read.GetProperty("status").GetInt32());
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Confirmed booking + agreement, tenant (with a signature image) already signed.
    /// Leaves the client authenticated as the tenant; returns the landlord's token.</summary>
    private async Task<(string AgreementId, string LandlordToken)> CreateAgreementWithSignedTenantAsync()
    {
        var (landlordId, landlordToken) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(landlordId);

        var res = await _httpClient.PostAsJsonAsync("/api/properties", new CreatePropertyRequest
        {
            Title = "Agreement Test Home",
            Description = "For signature tests",
            Location = "Accra, Ghana",
            Latitude = 5.6,
            Longitude = -0.19,
            Bedrooms = 1,
            Bathrooms = 1,
            MonthlyRent = 2000m,
            DailyRate = 100m,
            PropertyType = "Apartment",
            StayType = StayType.ShortTerm,
            CancellationPolicy = CancellationPolicy.Moderate
        });
        var propertyId = JsonDocument.Parse(await res.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("propertyId").GetString()!;

        var (tenantId, _) = await RegisterAndLoginAsync(UserRole.Tenant);

        // Tenant sets up their drawn signature before signing anything.
        Assert.Equal(HttpStatusCode.OK,
            (await _httpClient.PostAsync("/api/profile/signature", SignatureForm())).StatusCode);

        var checkIn = DateTime.UtcNow.Date.AddDays(10);
        var booked = await DataOf(await _httpClient.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            checkInDate = checkIn.ToString("yyyy-MM-dd"),
            checkOutDate = checkIn.AddDays(3).ToString("yyyy-MM-dd"),
            guests = 1
        }));
        var bookingId = booked.GetProperty("bookingId").GetString()!;

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var booking = await db.Bookings.FindAsync(bookingId);
            booking!.Status = BookingStatus.Confirmed; // agreements need a paid booking
            await db.SaveChangesAsync();
        }

        var agreement = await DataOf(await _httpClient.PostAsJsonAsync("/api/agreements", new { bookingId }));
        var agreementId = agreement.GetProperty("agreementId").GetString()!;

        Assert.Equal(HttpStatusCode.OK,
            (await _httpClient.PostAsync($"/api/agreements/{agreementId}/sign", null)).StatusCode);
        Assert.NotNull(tenantId);
        return (agreementId, landlordToken);
    }

    private void UseToken(string token) =>
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    private static async Task<JsonElement> DataOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data");
    }
}
