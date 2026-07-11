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

        // Imported ranges die with their feed; manual blocks (null FK) are unaffected.
        builder.HasOne(e => e.ExternalCalendar)
            .WithMany()
            .HasForeignKey(e => e.ExternalCalendarId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.ExternalCalendarId);
    }
}

public class ExternalCalendarConfiguration : IEntityTypeConfiguration<ExternalCalendar>
{
    public void Configure(EntityTypeBuilder<ExternalCalendar> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.FeedUrl).IsRequired().HasMaxLength(2000);
        builder.Property(e => e.LastSyncError).HasMaxLength(1000);

        builder.HasOne(e => e.Property)
            .WithMany()
            .HasForeignKey(e => e.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.PropertyId);
    }
}
