using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TripNest.Core.Models;

namespace TripNest.Core.Configurations;

public class AgreementConfiguration : IEntityTypeConfiguration<Agreement>
{
    public void Configure(EntityTypeBuilder<Agreement> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.BookingId).IsRequired().HasMaxLength(36);
        builder.Property(a => a.TermsContent).IsRequired();
        builder.Property(a => a.Status).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.TenantSignature).HasMaxLength(500);
        builder.Property(a => a.LandlordSignature).HasMaxLength(500);

        builder.HasOne(a => a.Booking)
            .WithMany()
            .HasForeignKey(a => a.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.BookingId).IsUnique();
        builder.HasIndex(a => a.Status);
    }
}

public class MaintenanceConfiguration : IEntityTypeConfiguration<Maintenance>
{
    public void Configure(EntityTypeBuilder<Maintenance> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.PropertyId).IsRequired().HasMaxLength(36);
        builder.Property(m => m.ReportedByUserId).IsRequired().HasMaxLength(36);
        builder.Property(m => m.Description).IsRequired();
        builder.Property(m => m.Status).IsRequired();
        builder.Property(m => m.PhotoPath).HasMaxLength(500);
        builder.Property(m => m.Resolution).HasMaxLength(1000);

        builder.HasOne(m => m.Property)
            .WithMany()
            .HasForeignKey(m => m.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.ReportedByUser)
            .WithMany()
            .HasForeignKey(m => m.ReportedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.PropertyId);
        builder.HasIndex(m => m.Status);
        builder.HasIndex(m => m.CreatedAt);
    }
}

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.ReviewerId).IsRequired().HasMaxLength(36);
        builder.Property(r => r.RevieweeId).IsRequired().HasMaxLength(36);
        builder.Property(r => r.PropertyId).IsRequired().HasMaxLength(36);
        builder.Property(r => r.Rating).IsRequired();
        builder.Property(r => r.Comment).IsRequired();

        builder.HasOne(r => r.Reviewer)
            .WithMany()
            .HasForeignKey(r => r.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Reviewee)
            .WithMany()
            .HasForeignKey(r => r.RevieweeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Property)
            .WithMany()
            .HasForeignKey(r => r.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.RevieweeId);
        builder.HasIndex(r => r.PropertyId);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.UserId).IsRequired().HasMaxLength(36);
        builder.Property(n => n.Title).IsRequired().HasMaxLength(200);
        builder.Property(n => n.Message).IsRequired();

        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => n.UserId);
        builder.HasIndex(n => n.IsRead);
        builder.HasIndex(n => n.CreatedAt);
    }
}

public class CaretakerConfiguration : IEntityTypeConfiguration<Caretaker>
{
    public void Configure(EntityTypeBuilder<Caretaker> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.UserId).IsRequired().HasMaxLength(36);
        builder.Property(c => c.PropertyId).IsRequired().HasMaxLength(36);
        builder.Property(c => c.Status).IsRequired();
        builder.Property(c => c.MonthlyCompensation).HasPrecision(18, 2);
        builder.Property(c => c.Responsibilities).IsRequired();

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Property)
            .WithMany()
            .HasForeignKey(c => c.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.PropertyId);
        builder.HasIndex(c => c.UserId);
    }
}
