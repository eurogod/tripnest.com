using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TripNest.Core.Models;

namespace TripNest.Core.Configurations;

public class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.UserId).IsRequired().HasMaxLength(36);
        builder.Property(p => p.Title).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).IsRequired().HasColumnType("text");
        builder.Property(p => p.Location).IsRequired().HasMaxLength(500);
        builder.Property(p => p.Latitude).IsRequired();
        builder.Property(p => p.Longitude).IsRequired();
        builder.Property(p => p.Bedrooms).IsRequired();
        builder.Property(p => p.Bathrooms).IsRequired();
        builder.Property(p => p.MonthlyRent).IsRequired().HasPrecision(18, 2);
        builder.Property(p => p.DailyRate).HasPrecision(18, 2);
        builder.Property(p => p.PropertyType).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Amenities).HasColumnType("text");
        builder.Property(p => p.PhotoPaths).HasColumnType("text");
        builder.Property(p => p.Status).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Walkthroughs)
            .WithOne(w => w.Property)
            .HasForeignKey(w => w.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.UserId);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.CreatedAt);
        builder.HasIndex(p => new { p.Latitude, p.Longitude });
    }
}

public class WalkthroughConfiguration : IEntityTypeConfiguration<Walkthrough>
{
    public void Configure(EntityTypeBuilder<Walkthrough> builder)
    {
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();

        builder.Property(w => w.PropertyId).IsRequired().HasMaxLength(36);
        builder.Property(w => w.Title).IsRequired().HasMaxLength(200);
        builder.Property(w => w.VideoUrl).IsRequired().HasMaxLength(500);
        builder.Property(w => w.ThumbnailUrl).HasMaxLength(500);
        builder.Property(w => w.DurationSeconds).IsRequired();
        builder.Property(w => w.CreatedAt).IsRequired();

        builder.HasOne(w => w.Property)
            .WithMany(p => p.Walkthroughs)
            .HasForeignKey(w => w.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(w => w.PropertyId);
        builder.HasIndex(w => w.CreatedAt);
    }
}
