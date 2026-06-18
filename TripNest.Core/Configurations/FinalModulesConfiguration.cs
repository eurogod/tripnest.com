using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TripNest.Core.Models;

namespace TripNest.Core.Configurations;

public class SafetyCheckInConfiguration : IEntityTypeConfiguration<SafetyCheckIn>
{
    public void Configure(EntityTypeBuilder<SafetyCheckIn> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.BookingId).IsRequired().HasMaxLength(36);
        builder.Property(s => s.EmergencyContactPhone).HasMaxLength(20);

        builder.HasOne(s => s.Booking)
            .WithMany()
            .HasForeignKey(s => s.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.BookingId).IsUnique();
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.UserId).IsRequired().HasMaxLength(36);
        builder.Property(a => a.Action).IsRequired().HasMaxLength(100);
        builder.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.EntityId).IsRequired().HasMaxLength(100);

        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.CreatedAt);
    }
}

public class TrustScoreSnapshotConfiguration : IEntityTypeConfiguration<TrustScoreSnapshot>
{
    public void Configure(EntityTypeBuilder<TrustScoreSnapshot> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.SubjectType).IsRequired().HasMaxLength(50);
        builder.Property(t => t.SubjectId).IsRequired().HasMaxLength(36);
        builder.Property(t => t.VerificationComponent).HasPrecision(5, 2);
        builder.Property(t => t.HistoryComponent).HasPrecision(5, 2);
        builder.Property(t => t.FeedbackComponent).HasPrecision(5, 2);
        builder.Property(t => t.FinalScore).HasPrecision(5, 2);

        builder.HasIndex(t => new { t.SubjectType, t.SubjectId, t.SnapshotDate }).IsUnique();
    }
}

public class StayFeedbackConfiguration : IEntityTypeConfiguration<StayFeedback>
{
    public void Configure(EntityTypeBuilder<StayFeedback> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.BookingId).IsRequired().HasMaxLength(36);
        builder.Property(s => s.PropertyId).IsRequired().HasMaxLength(36);
        builder.Property(s => s.LandlordId).IsRequired().HasMaxLength(36);
        builder.Property(s => s.TenantId).IsRequired().HasMaxLength(36);
        builder.Property(s => s.Comment).HasMaxLength(1000);

        builder.HasOne(s => s.Booking)
            .WithMany()
            .HasForeignKey(s => s.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Property)
            .WithMany()
            .HasForeignKey(s => s.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Landlord)
            .WithMany()
            .HasForeignKey(s => s.LandlordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Tenant)
            .WithMany()
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.BookingId).IsUnique();
        builder.HasIndex(s => s.PropertyId);
        builder.HasIndex(s => s.LandlordId);
    }
}
