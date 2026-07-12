using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TripNest.Core.Models;

namespace TripNest.Core.Configurations;

public class PayoutConfiguration : IEntityTypeConfiguration<Payout>
{
    public void Configure(EntityTypeBuilder<Payout> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        // Exactly one payout per source — the DB backstop for the service's idempotency checks
        // (Postgres unique indexes ignore NULLs, so escrow- and rent-sourced payouts coexist).
        builder.HasIndex(p => p.EscrowId).IsUnique();
        builder.HasIndex(p => p.RentInvoiceId).IsUnique();
        builder.HasIndex(p => p.DamageClaimId).IsUnique();
        builder.HasIndex(p => p.LandlordId);
        builder.HasIndex(p => p.Status);

        builder.Property(p => p.GrossAmount).HasColumnType("numeric(12,2)");
        builder.Property(p => p.FeeAmount).HasColumnType("numeric(12,2)");
        builder.Property(p => p.Amount).HasColumnType("numeric(12,2)");
        builder.Property(p => p.TransferCode).HasMaxLength(100);
        builder.Property(p => p.FailureReason).HasMaxLength(500);

        builder.HasOne(p => p.Escrow)
            .WithMany()
            .HasForeignKey(p => p.EscrowId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.RentInvoice)
            .WithMany()
            .HasForeignKey(p => p.RentInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.DamageClaim)
            .WithMany()
            .HasForeignKey(p => p.DamageClaimId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PayoutAccountConfiguration : IEntityTypeConfiguration<PayoutAccount>
{
    public void Configure(EntityTypeBuilder<PayoutAccount> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        // One payout destination per user.
        builder.HasIndex(a => a.UserId).IsUnique();

        builder.Property(a => a.Channel).IsRequired().HasMaxLength(20);
        builder.Property(a => a.ProviderCode).IsRequired().HasMaxLength(20);
        builder.Property(a => a.AccountNumber).IsRequired().HasMaxLength(50);
        builder.Property(a => a.AccountName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.RecipientCode).HasMaxLength(100);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
