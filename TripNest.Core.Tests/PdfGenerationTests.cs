using System.Text;
using QuestPDF.Infrastructure;
using TripNest.Core.Enums;
using TripNest.Core.Models;
using TripNest.Core.Pdf;

namespace TripNest.Core.Tests;

/// <summary>
/// Verifies the agreement/receipt renderers emit real PDF bytes (magic header `%PDF`) and that the
/// QuestPDF layout composes without throwing — composition errors only surface at render time.
/// </summary>
public class PdfGenerationTests
{
    static PdfGenerationTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static string Magic(byte[] bytes) => Encoding.ASCII.GetString(bytes, 0, 4);

    [Fact]
    public void AgreementPdf_RendersValidPdf()
    {
        var agreement = new Agreement
        {
            BookingId = "b1",
            TermsContent = "The tenant agrees to pay rent on time and keep the property in good order.",
            RentAmount = 1200m,
            RentFrequency = RentFrequency.Monthly,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(12),
            Status = AgreementStatus.Signed,
            TenantSignature = "John Tenant",
            LandlordSignature = "Jane Landlord",
            SignedAt = DateTime.UtcNow
        };
        var booking = new Booking
        {
            TenantId = "t1",
            PropertyId = "p1",
            Property = new Property
            {
                UserId = "u1", Title = "Cozy 2-bed in East Legon", Description = "Nice", Location = "Accra",
                Latitude = 5.6, Longitude = -0.18, Bedrooms = 2, Bathrooms = 1,
                MonthlyRent = 1200m, DailyRate = null, PropertyType = "Apartment"
            },
            Tenant = new User
            {
                FullName = "John Tenant", Email = "john@example.com", PasswordHash = "x",
                Phone = "+233200000000", Role = UserRole.Tenant
            }
        };

        var bytes = AgreementPdf.Render(agreement, booking);

        Assert.Equal("%PDF", Magic(bytes));
        Assert.True(bytes.Length > 1000);
    }

    [Theory]
    [InlineData(true)]   // with a (fake) photo
    [InlineData(false)]  // initials placeholder
    public void IdCardPdf_RendersValidPdf(bool withPhoto)
    {
        var user = new User
        {
            FullName = "Ama Mensah", Email = "ama@example.com", PasswordHash = "x",
            Phone = "+233200000000", Role = UserRole.Landlord,
            IsVerified = true, TripNestId = "TN-GH-2026-000042", CreatedAt = DateTime.UtcNow
        };

        // A 1x1 PNG is enough to exercise the image path without a real file.
        byte[]? photo = withPhoto
            ? Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M8AAAMBAQDJ/pLvAAAAAElFTkSuQmCC")
            : null;

        var bytes = IdCardPdf.Render(user, photo);

        Assert.Equal("%PDF", Magic(bytes));
        Assert.True(bytes.Length > 1000);
    }

    [Fact]
    public void ReceiptPdf_RendersValidPdf()
    {
        var receipt = new Receipt
        {
            BookingId = "b1", UserId = "u1", Amount = 500m,
            PaymentMethod = "Paystack", Description = "Security deposit", CreatedAt = DateTime.UtcNow
        };

        var bytes = ReceiptPdf.Render(receipt, "John Tenant");

        Assert.Equal("%PDF", Magic(bytes));
        Assert.True(bytes.Length > 1000);
    }
}
