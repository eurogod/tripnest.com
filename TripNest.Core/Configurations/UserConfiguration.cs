using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TripNest.Core.Models;

namespace TripNest.Core.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.Phone)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(u => u.Role)
            .IsRequired();

        builder.Property(u => u.IsVerified)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.TripNestId)
            .IsRequired(false)
            .HasMaxLength(50);

        // Public handle must be unambiguous — two accounts must never share a username.
        // (Postgres treats NULLs as distinct, so users without a handle are unaffected.)
        builder.Property(u => u.Username)
            .IsRequired(false)
            .HasMaxLength(50);
        builder.HasIndex(u => u.Username).IsUnique();

        builder.Property(u => u.ProfilePhotoPath)
            .IsRequired(false)
            .HasMaxLength(500);

        builder.Property(u => u.Bio)
            .IsRequired(false)
            .HasColumnType("text");

        builder.Property(u => u.RefreshToken)
            .IsRequired(false)
            .HasColumnType("text");

        builder.Property(u => u.PasswordResetToken)
            .IsRequired(false)
            .HasColumnType("text");

        builder.Property(u => u.PasswordResetTokenExpiry)
            .IsRequired(false);

        builder.Property(u => u.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(u => u.LastLoginAt)
            .IsRequired(false);

        builder.Property(u => u.RefreshTokenExpiryTime)
            .IsRequired(false);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.HasIndex(u => u.TripNestId)
            .IsUnique();

        builder.HasIndex(u => u.CreatedAt);
        builder.HasIndex(u => u.IsActive);
    }
}
