using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TripNest.Core.Models;

namespace TripNest.Core.Configurations;

public class BookingShareConfiguration : IEntityTypeConfiguration<BookingShare>
{
    public void Configure(EntityTypeBuilder<BookingShare> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Amount).HasPrecision(18, 2);
        builder.Property(s => s.PaymentReference).HasMaxLength(200);

        builder.HasOne(s => s.Booking)
            .WithMany()
            .HasForeignKey(s => s.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Money history: a participant's payment record must outlive account deactivation flows.
        builder.HasOne(s => s.Participant)
            .WithMany()
            .HasForeignKey(s => s.ParticipantUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.BookingId);
        // One share per member per booking.
        builder.HasIndex(s => new { s.BookingId, s.ParticipantUserId }).IsUnique();
    }
}
