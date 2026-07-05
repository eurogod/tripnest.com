using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TripNest.Core.Models;

namespace TripNest.Core.Configurations;

public class EscrowEventConfiguration : IEntityTypeConfiguration<EscrowEvent>
{
    public void Configure(EntityTypeBuilder<EscrowEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Actor).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Reason).HasMaxLength(1000);

        // Restrict, not Cascade: this is the audit trail — deleting an escrow must not be able to
        // silently take its transition history with it.
        builder.HasOne(e => e.Escrow)
            .WithMany()
            .HasForeignKey(e => e.EscrowId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.EscrowId);
        builder.HasIndex(e => e.BookingId);
    }
}
