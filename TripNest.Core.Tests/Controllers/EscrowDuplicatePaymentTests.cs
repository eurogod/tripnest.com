using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// A tenant can complete two checkouts for the same booking (re-initiating a Pending escrow mints
/// a fresh reference). The second successful charge must be auto-refunded at the provider — never
/// silently stranded — while the escrow stays held under the first reference.
/// </summary>
public class EscrowDuplicatePaymentTests : TestBase
{
    [Fact]
    public async Task SecondChargeForHeldEscrow_IsAutoRefunded_AndEscrowStaysHeld()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);

        string bookingId, escrowId;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var property = new Property
            {
                UserId = "landlord-dup",
                Title = "Duplicate Payment Test Property",
                Description = "d",
                Location = "Accra",
                Latitude = 5.6,
                Longitude = -0.2,
                Bedrooms = 1,
                Bathrooms = 1,
                MonthlyRent = 900m,
                DailyRate = 40m,
                PropertyType = "Apartment"
            };
            db.Set<Property>().Add(property);
            var booking = new Booking
            {
                TenantId = userId,
                PropertyId = property.Id,
                TotalAmount = 500m,
                Status = BookingStatus.Confirmed
            };
            db.Set<Booking>().Add(booking);
            bookingId = booking.Id;
            var escrow = new Escrow
            {
                BookingId = booking.Id,
                Amount = 500m,
                Status = EscrowStatus.HeldInEscrow,
                PaymentReference = "REF-FIRST",
                HeldAt = DateTime.UtcNow
            };
            db.Set<Escrow>().Add(escrow);
            escrowId = escrow.Id;
            await db.SaveChangesAsync();
        }

        var stub = _fixture.Services.GetRequiredService<StubPaymentGateway>();

        // The provider reports a SECOND successful charge for the same booking (different reference).
        using (var scope = _fixture.Services.CreateScope())
        {
            var escrowService = scope.ServiceProvider.GetRequiredService<IEscrowService>();
            await escrowService.VerifyAndHoldPaymentAsync(bookingId, "REF-DUPLICATE", 500m);
        }

        // The duplicate charge was refunded at the provider for the full paid amount…
        Assert.Contains(stub.Refunds, r => r.Reference == "REF-DUPLICATE" && r.Amount == 500m);

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // …the escrow is untouched: still held, still under the first reference…
            var escrow = await db.Set<Escrow>().FindAsync(escrowId);
            Assert.Equal(EscrowStatus.HeldInEscrow, escrow!.Status);
            Assert.Equal("REF-FIRST", escrow.PaymentReference);

            // …and the refund is on the audit trail for reconciliation.
            var auditNote = db.Set<EscrowEvent>().Single(e => e.EscrowId == escrowId && e.Reason!.Contains("REF-DUPLICATE"));
            Assert.Equal(EscrowStatus.HeldInEscrow, auditNote.FromStatus);
            Assert.Equal(EscrowStatus.HeldInEscrow, auditNote.ToStatus);
            Assert.Equal("payment-provider", auditNote.Actor);
        }
    }
}
