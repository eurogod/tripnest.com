using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TripNest.Core.Models;

namespace TripNest.Core.Configurations;

public class PropertyBlockedDateConfiguration : IEntityTypeConfiguration<PropertyBlockedDate>
{
    public void Configure(EntityTypeBuilder<PropertyBlockedDate> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne(e => e.Property)
            .WithMany()
            .HasForeignKey(e => e.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.BlockedByUser)
            .WithMany()
            .HasForeignKey(e => e.BlockedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
