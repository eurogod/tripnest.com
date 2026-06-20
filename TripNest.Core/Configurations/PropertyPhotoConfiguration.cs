using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TripNest.Core.Models;

namespace TripNest.Core.Configurations;

public class PropertyPhotoConfiguration : IEntityTypeConfiguration<PropertyPhoto>
{
    public void Configure(EntityTypeBuilder<PropertyPhoto> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.PhotoPath).IsRequired();
        builder.HasOne(p => p.Property)
            .WithMany(p => p.Photos)
            .HasForeignKey(p => p.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(p => p.PropertyId);
    }
}
