using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Marketplace;

// ---------------------------------------------------------------------------
// Pricing settings (per listing)
// ---------------------------------------------------------------------------
public class PricingSettingsResponse
{
    public required string PropertyId { get; set; }
    public decimal BaseRate { get; set; }
    public decimal WeekendRate { get; set; }
    public decimal WeeklyDiscountPercent { get; set; }
    public decimal MonthlyDiscountPercent { get; set; }
    public int MinNights { get; set; }
    public decimal CleaningFee { get; set; }
}

public class UpdatePricingSettingsRequest
{
    public decimal BaseRate { get; set; }
    public decimal WeekendRate { get; set; }
    public decimal WeeklyDiscountPercent { get; set; }
    public decimal MonthlyDiscountPercent { get; set; }
    public int MinNights { get; set; } = 1;
    public decimal CleaningFee { get; set; }
}

// ---------------------------------------------------------------------------
// Rate calendar
// ---------------------------------------------------------------------------
public class CalendarDayResponse
{
    public DateTime Date { get; set; }
    public decimal Price { get; set; }
    public bool IsWeekend { get; set; }
    public bool IsDiscounted { get; set; }
    public bool IsOwnerBlocked { get; set; }
    public bool IsMaintenance { get; set; }
    public bool IsBooked { get; set; }
}

public class CalendarMonthResponse
{
    public required string PropertyId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;
    public int MinNights { get; set; }
    public List<CalendarDayResponse> Days { get; set; } = new();
}

// ---------------------------------------------------------------------------
// Landlord inquiries
// ---------------------------------------------------------------------------
public class InquiryResponse
{
    public required string InquiryId { get; set; }
    public required string PropertyId { get; set; }
    public string? PropertyTitle { get; set; }
    public required string GuestName { get; set; }
    public required string Message { get; set; }
    public InquiryStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateInquiryRequest
{
    public required string PropertyId { get; set; }
    public string? GuestName { get; set; }
    public required string Message { get; set; }
}

public class UpdateInquiryStatusRequest
{
    public required string Status { get; set; }
}

// ---------------------------------------------------------------------------
// Saved payment methods
// ---------------------------------------------------------------------------
public class PaymentMethodResponse
{
    public required string Id { get; set; }
    public required string Provider { get; set; }
    public required string MaskedNumber { get; set; }
    public required string Channel { get; set; }
    public bool IsPrimary { get; set; }
}

public class CreatePaymentMethodRequest
{
    public required string Provider { get; set; }
    public required string MaskedNumber { get; set; }
    public required string Channel { get; set; }
    public bool MakePrimary { get; set; }
}

// ---------------------------------------------------------------------------
// Owner Exchange
// ---------------------------------------------------------------------------
public class ExchangePostResponse
{
    public required string Id { get; set; }
    public required string AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public required string Title { get; set; }
    public required string Body { get; set; }
    public ExchangeCategory Category { get; set; }
    public bool Pinned { get; set; }
    public int ReplyCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ExchangeReplyResponse
{
    public required string Id { get; set; }
    public required string AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public required string Body { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateExchangePostRequest
{
    public required string Title { get; set; }
    public required string Body { get; set; }
    public string Category { get; set; } = "General";
}

public class CreateExchangeReplyRequest
{
    public required string Body { get; set; }
}

// ---------------------------------------------------------------------------
// Host tasks
// ---------------------------------------------------------------------------
public class HostTaskResponse
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public string? PropertyId { get; set; }
    public HostTaskType Type { get; set; }
    public HostTaskPriority Priority { get; set; }
    public HostTaskStatus Status { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Assignee { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateHostTaskRequest
{
    public required string Title { get; set; }
    public string? PropertyId { get; set; }
    public string Type { get; set; } = "Cleaning";
    public string Priority { get; set; } = "Medium";
    public DateTime? DueDate { get; set; }
    public string? Assignee { get; set; }
}

public class UpdateHostTaskRequest
{
    public string? Title { get; set; }
    public string? Type { get; set; }
    public string? Priority { get; set; }
    public string? Status { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Assignee { get; set; }
}

// ---------------------------------------------------------------------------
// Team members
// ---------------------------------------------------------------------------
public class TeamMemberResponse
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public TeamMemberRole Role { get; set; }
    public TeamMemberStatus Status { get; set; }
    public int PropertiesCount { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class InviteTeamMemberRequest
{
    public required string Name { get; set; }
    public required string Email { get; set; }
    public string Role { get; set; } = "CoHost";
    public int PropertiesCount { get; set; }
}

public class UpdateTeamMemberRequest
{
    public string? Role { get; set; }
    public string? Status { get; set; }
    public int? PropertiesCount { get; set; }
}

// ---------------------------------------------------------------------------
// Resources
// ---------------------------------------------------------------------------
public class ResourceResponse
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public ResourceCategory Category { get; set; }
    public required string Format { get; set; }
    public required string Url { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateResourceRequest
{
    public required string Title { get; set; }
    public required string Description { get; set; }
    public string Category { get; set; } = "Guide";
    public required string Format { get; set; }
    public required string Url { get; set; }
}

// ---------------------------------------------------------------------------
// Statements (computed monthly payout summaries)
// ---------------------------------------------------------------------------
public class StatementResponse
{
    public required string Id { get; set; }
    public required string Month { get; set; }
    public required string Period { get; set; }
    public decimal GrossRevenue { get; set; }
    public decimal ManagementFee { get; set; }
    public decimal NetPayout { get; set; }
    public StatementStatus Status { get; set; }
}

// ---------------------------------------------------------------------------
// Virtual tour
// ---------------------------------------------------------------------------
public class TourHotspotDto
{
    public required string Id { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public required string Label { get; set; }
    public string Category { get; set; } = "amenity";
    public string Detail { get; set; } = string.Empty;
}

public class TourRoomDto
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string Area { get; set; } = "Indoor";
    public string Caption { get; set; } = string.Empty;
    public string? Dimensions { get; set; }
    public string? Media { get; set; }
    public List<TourHotspotDto> Hotspots { get; set; } = new();
}

public class PropertyTourResponse
{
    public required string PropertyId { get; set; }
    public required string Title { get; set; }
    public List<TourRoomDto> Rooms { get; set; } = new();
}

public class UpsertPropertyTourRequest
{
    public required string Title { get; set; }
    public List<TourRoomDto> Rooms { get; set; } = new();
}

// ---------------------------------------------------------------------------
// Landlord workspace: incoming bookings & tenant roster
// ---------------------------------------------------------------------------
public class LandlordBookingResponse
{
    public required string BookingId { get; set; }
    public required string PropertyId { get; set; }
    public string? Listing { get; set; }
    public string? Guest { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Nights { get; set; }
    public int Guests { get; set; }
    public decimal Amount { get; set; }
    public BookingStatus Status { get; set; }

    /// <summary>Display stage derived from status + dates: Upcoming / Active / Complete / Canceled —
    /// what the reservations table shows without every client re-deriving it.</summary>
    public required string Stage { get; set; }
}

/// <summary>
/// Everything the host's "Reservation Details" panel shows for one booking: the trip facts,
/// the guest, the earnings breakdown, and any reviews the guest left for this listing.
/// </summary>
public class ReservationDetailsResponse
{
    public required string BookingId { get; set; }
    public required string PropertyId { get; set; }
    public string? Listing { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Nights { get; set; }
    public int Guests { get; set; }
    public string TripType { get; set; } = "Reservation";
    public string BookedThrough { get; set; } = "TripNest";
    public BookingStatus Status { get; set; }
    public required string Stage { get; set; }

    public required string GuestId { get; set; }
    public string? GuestName { get; set; }
    /// <summary>The guest's public TripNest identity id when they are identity-verified.</summary>
    public string? GuestTripNestId { get; set; }

    /// <summary>Average nightly rate actually paid (total / nights).</summary>
    public decimal NightlyRate { get; set; }
    /// <summary>Gross booking revenue (what the guest paid).</summary>
    public decimal NetRevenue { get; set; }
    public decimal ManagementFeePercent { get; set; }
    public decimal ManagementFee { get; set; }
    /// <summary>What the host receives after the platform's management fee.</summary>
    public decimal OwnerPayout { get; set; }

    /// <summary>Reviews this guest has written for this listing.</summary>
    public List<GuestReviewItem> GuestReviews { get; set; } = new();
}

public class GuestReviewItem
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LandlordTenantResponse
{
    public required string TenantId { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Property { get; set; }
    public DateTime Since { get; set; }
    public DateTime LeaseEnd { get; set; }
    public decimal MonthlyRent { get; set; }
    public string Standing { get; set; } = "current";
}
