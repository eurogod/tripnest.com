using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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
    public DbSet<ExternalCalendar> ExternalCalendars { get; set; }
    public DbSet<BookingShare> BookingShares { get; set; }
    public DbSet<RoommateProfile> RoommateProfiles { get; set; }
    public DbSet<WishlistItem> WishlistItems { get; set; }
    public DbSet<CommunicationPreference> CommunicationPreferences { get; set; }
    public DbSet<PropertyPhoto> PropertyPhotos { get; set; }
    public DbSet<PropertyCaretakerAssignment> PropertyCaretakerAssignments { get; set; }

    // Marketplace / operations modules (frontend parity).
    public DbSet<PricingSettings> PricingSettings { get; set; }
    public DbSet<Inquiry> Inquiries { get; set; }
    public DbSet<SavedPaymentMethod> SavedPaymentMethods { get; set; }
    public DbSet<ExchangePost> ExchangePosts { get; set; }
    public DbSet<ExchangeReply> ExchangeReplies { get; set; }
    public DbSet<HostTask> HostTasks { get; set; }
    public DbSet<TeamMember> TeamMembers { get; set; }
    public DbSet<ResourceItem> ResourceItems { get; set; }
    public DbSet<PropertyTour> PropertyTours { get; set; }

    // Host disbursements (Paystack Transfers).
    public DbSet<Payout> Payouts { get; set; }
    public DbSet<PayoutAccount> PayoutAccounts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        // Npgsql maps DateTime to "timestamp with time zone" and rejects Kind=Unspecified values
        // at write time. Client JSON routinely carries bare dates ("2026-08-04" → Unspecified),
        // so normalize the Kind here — the one boundary every entity passes through — instead of
        // remembering to SpecifyKind in each endpoint.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }
}

/// <summary>Writes: Unspecified is taken as UTC, Local is converted. Reads: stamped as UTC.</summary>
internal class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(
        v => v.Kind == DateTimeKind.Utc
            ? v
            : v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}

internal class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter() : base(
        v => v == null
            ? v
            : v.Value.Kind == DateTimeKind.Utc
                ? v
                : v.Value.Kind == DateTimeKind.Local
                    ? v.Value.ToUniversalTime()
                    : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc),
        v => v == null ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc))
    {
    }
}
