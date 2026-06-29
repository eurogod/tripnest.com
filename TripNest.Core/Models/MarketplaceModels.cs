using TripNest.Core.Enums;

namespace TripNest.Core.Models;

/// <summary>Per-listing pricing rules used by the pricing page and the rate calendar.</summary>
public class PricingSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string PropertyId { get; set; }
    public Property? Property { get; set; }
    public decimal BaseRate { get; set; }
    public decimal WeekendRate { get; set; }
    public decimal WeeklyDiscountPercent { get; set; }
    public decimal MonthlyDiscountPercent { get; set; }
    public int MinNights { get; set; } = 1;
    public decimal CleaningFee { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A pre-booking question a guest sends a landlord about a listing.</summary>
public class Inquiry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string PropertyId { get; set; }
    public Property? Property { get; set; }
    public required string LandlordId { get; set; }
    public User? Landlord { get; set; }
    public string? GuestUserId { get; set; }
    public required string GuestName { get; set; }
    public required string Message { get; set; }
    public InquiryStatus Status { get; set; } = InquiryStatus.New;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A payment instrument a user has saved (card or mobile money) for faster checkout.</summary>
public class SavedPaymentMethod
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string UserId { get; set; }
    public User? User { get; set; }
    public required string Provider { get; set; }
    public required string MaskedNumber { get; set; }
    public required string Channel { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A post on the Owner Exchange community board.</summary>
public class ExchangePost
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string AuthorId { get; set; }
    public User? Author { get; set; }
    public required string Title { get; set; }
    public required string Body { get; set; }
    public ExchangeCategory Category { get; set; } = ExchangeCategory.General;
    public bool Pinned { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ExchangeReply> Replies { get; set; } = new List<ExchangeReply>();
}

public class ExchangeReply
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string PostId { get; set; }
    public ExchangePost? Post { get; set; }
    public required string AuthorId { get; set; }
    public User? Author { get; set; }
    public required string Body { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>An operational task on a landlord's task board.</summary>
public class HostTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string LandlordId { get; set; }
    public User? Landlord { get; set; }
    public string? PropertyId { get; set; }
    public Property? Property { get; set; }
    public required string Title { get; set; }
    public HostTaskType Type { get; set; } = HostTaskType.Cleaning;
    public HostTaskPriority Priority { get; set; } = HostTaskPriority.Medium;
    public HostTaskStatus Status { get; set; } = HostTaskStatus.Todo;
    public DateTime? DueDate { get; set; }
    public string? Assignee { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A member of a landlord's team (co-host, cleaner, maintenance, agent).</summary>
public class TeamMember
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string LandlordId { get; set; }
    public User? Landlord { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public TeamMemberRole Role { get; set; } = TeamMemberRole.CoHost;
    public TeamMemberStatus Status { get; set; } = TeamMemberStatus.Invited;
    public int PropertiesCount { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A curated host resource (guide, policy, template, video).</summary>
public class ResourceItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Title { get; set; }
    public required string Description { get; set; }
    public ResourceCategory Category { get; set; } = ResourceCategory.Guide;
    public required string Format { get; set; }
    public required string Url { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A property's virtual tour. Rooms (with hotspots) are stored as a JSON document so the
/// scene graph can evolve without a migration and stays provider-agnostic for the test suite.
/// </summary>
public class PropertyTour
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string PropertyId { get; set; }
    public Property? Property { get; set; }
    public required string Title { get; set; }
    public string RoomsJson { get; set; } = "[]";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
