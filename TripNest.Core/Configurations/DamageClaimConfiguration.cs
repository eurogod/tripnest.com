using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TripNest.Core.Models;

namespace TripNest.Core.Configurations;

public class DamageClaimConfiguration : IEntityTypeConfiguration<DamageClaim>
{
    public void Configure(EntityTypeBuilder<DamageClaim> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Amount).HasPrecision(18, 2);
        builder.Property(c => c.ApprovedAmount).HasPrecision(18, 2);
        builder.Property(c => c.Description).IsRequired().HasMaxLength(2000);
        builder.Property(c => c.TenantResponse).HasMaxLength(2000);
        builder.Property(c => c.ResolutionNote).HasMaxLength(1000);
        builder.Property(c => c.PhotoPaths).HasMaxLength(4000);

        builder.HasOne(c => c.Booking)
            .WithMany()
            .HasForeignKey(c => c.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        // One claim per booking; disputes about the same stay belong on one record.
        builder.HasIndex(c => c.BookingId).IsUnique();
        builder.HasIndex(c => c.LandlordId);
        builder.HasIndex(c => c.Status);
    }
}
