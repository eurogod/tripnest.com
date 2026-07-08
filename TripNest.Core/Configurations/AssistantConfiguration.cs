using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TripNest.Core.Models;

namespace TripNest.Core.Configurations;

public class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Subject).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Summary).IsRequired().HasMaxLength(4000);

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // The admin queue reads open tickets, oldest first.
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.UserId);
    }
}

public class AssistantMessageConfiguration : IEntityTypeConfiguration<AssistantMessage>
{
    public void Configure(EntityTypeBuilder<AssistantMessage> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Content).IsRequired().HasMaxLength(8000);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // History is always read per user, newest window first.
        builder.HasIndex(m => new { m.UserId, m.CreatedAt });
    }
}
