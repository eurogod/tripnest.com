using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TripNest.Core.Models;

namespace TripNest.Core.Configurations;

public class RentInvoiceConfiguration : IEntityTypeConfiguration<RentInvoice>
{
    public void Configure(EntityTypeBuilder<RentInvoice> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Amount).HasPrecision(18, 2);
        builder.Property(i => i.PaymentReference).HasMaxLength(200);

        builder.HasOne(i => i.Booking)
            .WithMany()
            .HasForeignKey(i => i.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.BookingId);
        builder.HasIndex(i => i.TenantId);
        builder.HasIndex(i => new { i.Status, i.DueDate }); // the daily due/overdue sweep
        // One invoice per booking per period.
        builder.HasIndex(i => new { i.BookingId, i.PeriodStart }).IsUnique();
    }
}
