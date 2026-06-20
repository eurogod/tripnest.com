using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TripNest.Core.Models;

namespace TripNest.Core.Configurations;

public class PropertyCaretakerAssignmentConfiguration : IEntityTypeConfiguration<PropertyCaretakerAssignment>
{
    public void Configure(EntityTypeBuilder<PropertyCaretakerAssignment> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.HasOne(a => a.Property)
            .WithMany()
            .HasForeignKey(a => a.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(a => a.Caretaker)
            .WithMany()
            .HasForeignKey(a => a.CaretakerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.PropertyId);
        builder.HasIndex(a => a.CaretakerId);
    }
}
