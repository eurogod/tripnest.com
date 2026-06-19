using Microsoft.EntityFrameworkCore;
using TripNest.Core.Models;

namespace TripNest.Core.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<VerificationRequest> VerificationRequests { get; set; }
    public DbSet<Property> Properties { get; set; }
    public DbSet<Walkthrough> Walkthroughs { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<Escrow> Escrows { get; set; }
    public DbSet<Agreement> Agreements { get; set; }
    public DbSet<Maintenance> Maintenances { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Caretaker> Caretakers { get; set; }
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Agent> Agents { get; set; }
    public DbSet<Receipt> Receipts { get; set; }
    public DbSet<SafetyCheckIn> SafetyCheckIns { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<TrustScoreSnapshot> TrustScoreSnapshots { get; set; }
    public DbSet<StayFeedback> StayFeedbacks { get; set; }
    public DbSet<ServiceRequest> ServiceRequests { get; set; }
    public DbSet<ViewingRequest> ViewingRequests { get; set; }
    public DbSet<PropertyBlockedDate> PropertyBlockedDates { get; set; }
    public DbSet<WishlistItem> WishlistItems { get; set; }
    public DbSet<CommunicationPreference> CommunicationPreferences { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
