using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TripNest.Core.Models;

namespace TripNest.Core.Configurations;

public class RoommateProfileConfiguration : IEntityTypeConfiguration<RoommateProfile>
{
    public void Configure(EntityTypeBuilder<RoommateProfile> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Bio).HasMaxLength(500);
        builder.Property(p => p.University).HasMaxLength(150);
        builder.Property(p => p.PreferredLocation).IsRequired().HasMaxLength(200);
        builder.Property(p => p.MonthlyBudget).HasPrecision(18, 2);

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One roommate profile per user.
        builder.HasIndex(p => p.UserId).IsUnique();
        // The match query narrows by visibility + location.
        builder.HasIndex(p => new { p.IsVisible, p.PreferredLocation });
    }
}
