using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TripNest.Core.Models;

namespace TripNest.Core.Configurations;

public class VerificationConfiguration : IEntityTypeConfiguration<VerificationRequest>
{
    public void Configure(EntityTypeBuilder<VerificationRequest> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.UserId).IsRequired().HasMaxLength(36);
        builder.Property(v => v.GhanaCardNumber).IsRequired().HasMaxLength(50);
        builder.Property(v => v.SelfiePhotoPath).IsRequired().HasMaxLength(500);
        builder.Property(v => v.NiaPhotoUrl).IsRequired().HasMaxLength(500);
        builder.Property(v => v.FaceMatchScore).IsRequired(false);
        builder.Property(v => v.FailureReason).IsRequired(false).HasMaxLength(500);
        builder.Property(v => v.Status).IsRequired();
        // Use the DB clock as the default, not a constant baked at model-build time
        // (the old HasDefaultValue(DateTime.UtcNow) drifted on every migration).
        builder.Property(v => v.SubmittedAt).IsRequired().HasDefaultValueSql("now()");
        builder.Property(v => v.ReviewedAt).IsRequired(false);

        builder.HasOne(v => v.User)
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => v.UserId);
        builder.HasIndex(v => v.GhanaCardNumber).IsUnique();
        builder.HasIndex(v => v.Status);
        builder.HasIndex(v => v.SubmittedAt);
    }
}
