using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TripNest.Core.Models;

namespace TripNest.Core.Configurations;

public class PricingSettingsConfiguration : IEntityTypeConfiguration<PricingSettings>
{
    public void Configure(EntityTypeBuilder<PricingSettings> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.BaseRate).HasPrecision(18, 2);
        builder.Property(e => e.WeekendRate).HasPrecision(18, 2);
        builder.Property(e => e.WeeklyDiscountPercent).HasPrecision(5, 2);
        builder.Property(e => e.MonthlyDiscountPercent).HasPrecision(5, 2);
        builder.Property(e => e.CleaningFee).HasPrecision(18, 2);
        builder.HasIndex(e => e.PropertyId).IsUnique();

        builder.HasOne(e => e.Property)
            .WithMany()
            .HasForeignKey(e => e.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class InquiryConfiguration : IEntityTypeConfiguration<Inquiry>
{
    public void Configure(EntityTypeBuilder<Inquiry> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.GuestName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Message).IsRequired().HasColumnType("text");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        builder.HasIndex(e => e.LandlordId);

        builder.HasOne(e => e.Property)
            .WithMany()
            .HasForeignKey(e => e.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Landlord)
            .WithMany()
            .HasForeignKey(e => e.LandlordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SavedPaymentMethodConfiguration : IEntityTypeConfiguration<SavedPaymentMethod>
{
    public void Configure(EntityTypeBuilder<SavedPaymentMethod> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Provider).IsRequired().HasMaxLength(100);
        builder.Property(e => e.MaskedNumber).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Channel).IsRequired().HasMaxLength(20);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        builder.HasIndex(e => e.UserId);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExchangePostConfiguration : IEntityTypeConfiguration<ExchangePost>
{
    public void Configure(EntityTypeBuilder<ExchangePost> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Title).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Body).IsRequired().HasColumnType("text");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne(e => e.Author)
            .WithMany()
            .HasForeignKey(e => e.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Replies)
            .WithOne(r => r.Post)
            .HasForeignKey(r => r.PostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExchangeReplyConfiguration : IEntityTypeConfiguration<ExchangeReply>
{
    public void Configure(EntityTypeBuilder<ExchangeReply> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Body).IsRequired().HasColumnType("text");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne(e => e.Author)
            .WithMany()
            .HasForeignKey(e => e.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class HostTaskConfiguration : IEntityTypeConfiguration<HostTask>
{
    public void Configure(EntityTypeBuilder<HostTask> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Title).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Assignee).HasMaxLength(200);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        builder.HasIndex(e => e.LandlordId);

        builder.HasOne(e => e.Landlord)
            .WithMany()
            .HasForeignKey(e => e.LandlordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Property)
            .WithMany()
            .HasForeignKey(e => e.PropertyId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Email).IsRequired().HasMaxLength(256);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        builder.HasIndex(e => e.LandlordId);

        builder.HasOne(e => e.Landlord)
            .WithMany()
            .HasForeignKey(e => e.LandlordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ResourceItemConfiguration : IEntityTypeConfiguration<ResourceItem>
{
    public void Configure(EntityTypeBuilder<ResourceItem> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Title).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).IsRequired().HasColumnType("text");
        builder.Property(e => e.Format).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Url).IsRequired().HasMaxLength(1000);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
    }
}

public class PropertyTourConfiguration : IEntityTypeConfiguration<PropertyTour>
{
    public void Configure(EntityTypeBuilder<PropertyTour> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Title).IsRequired().HasMaxLength(200);
        builder.Property(e => e.RoomsJson).IsRequired().HasColumnType("text");
        builder.HasIndex(e => e.PropertyId).IsUnique();

        builder.HasOne(e => e.Property)
            .WithMany()
            .HasForeignKey(e => e.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
