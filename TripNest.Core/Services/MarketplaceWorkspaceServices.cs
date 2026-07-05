using System.Globalization;
using System.Text.Json;
using TripNest.Core.DTOs.Marketplace;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Enums;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

/// <summary>Normalises caller-supplied paging into safe bounds.</summary>
internal static class Paging
{
    public static (int page, int size) Clamp(int page, int pageSize, int maxSize = 100)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > maxSize) pageSize = maxSize;
        return (page, pageSize);
    }

    public static PagedResult<T> Page<T>(IReadOnlyList<T> all, int page, int pageSize)
    {
        var (p, size) = Clamp(page, pageSize);
        return new PagedResult<T>
        {
            Items = all.Skip((p - 1) * size).Take(size).ToList(),
            TotalCount = all.Count,
            Page = p,
            PageSize = size
        };
    }

    /// <summary>Wraps an already-paged slice (e.g. from <c>FindPageAsync</c>) with its total.</summary>
    public static PagedResult<T> Result<T>(IReadOnlyList<T> items, int totalCount, int page, int pageSize) => new()
    {
        Items = items,
        TotalCount = totalCount,
        Page = page,
        PageSize = pageSize
    };
}

public class InquiryService : IInquiryService
{
    private readonly IRepository<Inquiry> _inquiryRepository;
    private readonly IPropertyRepository _propertyRepository;

    public InquiryService(IRepository<Inquiry> inquiryRepository, IPropertyRepository propertyRepository)
    {
        _inquiryRepository = inquiryRepository;
        _propertyRepository = propertyRepository;
    }

    public async Task<InquiryResponse> CreateAsync(CreateInquiryRequest request, string? guestUserId, string? guestNameFallback)
    {
        var property = await _propertyRepository.GetByIdAsync(request.PropertyId)
            ?? throw new NotFoundException("Property");

        var inquiry = new Inquiry
        {
            PropertyId = property.Id,
            LandlordId = property.UserId,
            GuestUserId = guestUserId,
            GuestName = request.GuestName ?? guestNameFallback ?? "Guest",
            Message = request.Message
        };
        await _inquiryRepository.AddAsync(inquiry);
        await _inquiryRepository.SaveChangesAsync();
        return Map(inquiry, property.Title);
    }

    public async Task<PagedResult<InquiryResponse>> GetForLandlordAsync(string landlordId, int page, int pageSize)
    {
        var (pageNum, size) = Paging.Clamp(page, pageSize);
        var (inquiries, totalCount) = await _inquiryRepository.FindPageAsync(
            i => i.LandlordId == landlordId,
            q => q.OrderByDescending(i => i.CreatedAt),
            pageNum, size);

        var propertyIds = inquiries.Select(i => i.PropertyId).Distinct().ToList();
        var titles = (await _propertyRepository.FindAsync(p => propertyIds.Contains(p.Id)))
            .ToDictionary(p => p.Id, p => p.Title);

        var mapped = inquiries.Select(i => Map(i, titles.GetValueOrDefault(i.PropertyId))).ToList();
        return Paging.Result(mapped, totalCount, pageNum, size);
    }

    public async Task<InquiryResponse> UpdateStatusAsync(string inquiryId, string status, string landlordId)
    {
        var inquiry = await _inquiryRepository.GetByIdAsync(inquiryId) ?? throw new NotFoundException("Inquiry");
        if (inquiry.LandlordId != landlordId)
            throw new ForbiddenException("This inquiry is not yours");

        inquiry.Status = EnumParse.Required<InquiryStatus>(status, "status");
        await _inquiryRepository.UpdateAsync(inquiry);
        await _inquiryRepository.SaveChangesAsync();

        var property = await _propertyRepository.GetByIdAsync(inquiry.PropertyId);
        return Map(inquiry, property?.Title);
    }

    private static InquiryResponse Map(Inquiry i, string? title) => new()
    {
        InquiryId = i.Id,
        PropertyId = i.PropertyId,
        PropertyTitle = title,
        GuestName = i.GuestName,
        Message = i.Message,
        Status = i.Status,
        CreatedAt = i.CreatedAt
    };
}

public class PaymentMethodService : IPaymentMethodService
{
    private readonly IRepository<SavedPaymentMethod> _repository;

    public PaymentMethodService(IRepository<SavedPaymentMethod> repository) => _repository = repository;

    public async Task<List<PaymentMethodResponse>> GetMineAsync(string userId)
    {
        var methods = (await _repository.FindAsync(m => m.UserId == userId))
            .OrderByDescending(m => m.IsPrimary)
            .ThenByDescending(m => m.CreatedAt);
        return methods.Select(Map).ToList();
    }

    public async Task<PaymentMethodResponse> AddAsync(CreatePaymentMethodRequest request, string userId)
    {
        var existing = (await _repository.FindAsync(m => m.UserId == userId)).ToList();
        var makePrimary = request.MakePrimary || existing.Count == 0;

        if (makePrimary)
        {
            foreach (var m in existing.Where(m => m.IsPrimary))
            {
                m.IsPrimary = false;
                await _repository.UpdateAsync(m);
            }
        }

        var method = new SavedPaymentMethod
        {
            UserId = userId,
            Provider = request.Provider,
            MaskedNumber = request.MaskedNumber,
            Channel = request.Channel,
            IsPrimary = makePrimary
        };
        await _repository.AddAsync(method);
        await _repository.SaveChangesAsync();
        return Map(method);
    }

    public async Task SetPrimaryAsync(string id, string userId)
    {
        var all = (await _repository.FindAsync(m => m.UserId == userId)).ToList();
        var target = all.FirstOrDefault(m => m.Id == id) ?? throw new NotFoundException("Payment method");

        foreach (var m in all)
        {
            var shouldBePrimary = m.Id == target.Id;
            if (m.IsPrimary != shouldBePrimary)
            {
                m.IsPrimary = shouldBePrimary;
                await _repository.UpdateAsync(m);
            }
        }
        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id, string userId)
    {
        var method = await _repository.GetByIdAsync(id) ?? throw new NotFoundException("Payment method");
        if (method.UserId != userId)
            throw new ForbiddenException("This payment method is not yours");
        await _repository.DeleteAsync(method);
        await _repository.SaveChangesAsync();
    }

    private static PaymentMethodResponse Map(SavedPaymentMethod m) => new()
    {
        Id = m.Id,
        Provider = m.Provider,
        MaskedNumber = m.MaskedNumber,
        Channel = m.Channel,
        IsPrimary = m.IsPrimary
    };
}

public class StatementService : IStatementService
{
    // Platform management fee applied to gross booking revenue.
    private const decimal ManagementFeeRate = 0.10m;

    private readonly IPropertyRepository _propertyRepository;
    private readonly IBookingRepository _bookingRepository;

    public StatementService(IPropertyRepository propertyRepository, IBookingRepository bookingRepository)
    {
        _propertyRepository = propertyRepository;
        _bookingRepository = bookingRepository;
    }

    public async Task<List<StatementResponse>> GetForLandlordAsync(string landlordId)
    {
        var propertyIds = (await _propertyRepository.GetByUserIdAsync(landlordId)).Select(p => p.Id).ToList();
        if (propertyIds.Count == 0)
            return new List<StatementResponse>();

        // Single query for all of the landlord's bookings (no per-property round-trips).
        var bookings = (await _bookingRepository.FindAsync(b => propertyIds.Contains(b.PropertyId)))
            .Where(b => b.Status != BookingStatus.Cancelled);
        var now = DateTime.UtcNow;

        return bookings
            .GroupBy(b => new { b.CheckInDate.Year, b.CheckInDate.Month })
            .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
            .Select(g =>
            {
                var gross = g.Sum(b => b.TotalAmount);
                var fee = Math.Round(gross * ManagementFeeRate, 2);
                var monthStart = new DateTime(g.Key.Year, g.Key.Month, 1);
                var isPast = g.Key.Year < now.Year || (g.Key.Year == now.Year && g.Key.Month < now.Month);
                return new StatementResponse
                {
                    Id = $"{g.Key.Year:D4}-{g.Key.Month:D2}",
                    Month = monthStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                    Period = $"{monthStart:dd MMM} – {monthStart.AddMonths(1).AddDays(-1):dd MMM yyyy}",
                    GrossRevenue = gross,
                    ManagementFee = fee,
                    NetPayout = gross - fee,
                    Status = isPast ? StatementStatus.Paid : StatementStatus.Pending
                };
            })
            .ToList();
    }
}

public class TourService : ITourService
{
    private readonly IRepository<PropertyTour> _tourRepository;
    private readonly IPropertyRepository _propertyRepository;

    public TourService(IRepository<PropertyTour> tourRepository, IPropertyRepository propertyRepository)
    {
        _tourRepository = tourRepository;
        _propertyRepository = propertyRepository;
    }

    public async Task<PropertyTourResponse?> GetAsync(string propertyId)
    {
        var tour = (await _tourRepository.FindAsync(t => t.PropertyId == propertyId)).FirstOrDefault();
        if (tour is null) return null;

        return new PropertyTourResponse
        {
            PropertyId = tour.PropertyId,
            Title = tour.Title,
            Rooms = JsonSerializer.Deserialize<List<TourRoomDto>>(tour.RoomsJson) ?? new List<TourRoomDto>()
        };
    }

    public async Task<PropertyTourResponse> UpsertAsync(string propertyId, UpsertPropertyTourRequest request, string landlordId)
    {
        var property = await _propertyRepository.GetByIdAsync(propertyId) ?? throw new NotFoundException("Property");
        if (property.UserId != landlordId)
            throw new ForbiddenException("You do not own this listing");

        var roomsJson = JsonSerializer.Serialize(request.Rooms);
        var tour = (await _tourRepository.FindAsync(t => t.PropertyId == propertyId)).FirstOrDefault();
        if (tour is null)
        {
            tour = new PropertyTour { PropertyId = propertyId, Title = request.Title, RoomsJson = roomsJson };
            await _tourRepository.AddAsync(tour);
        }
        else
        {
            tour.Title = request.Title;
            tour.RoomsJson = roomsJson;
            tour.UpdatedAt = DateTime.UtcNow;
            await _tourRepository.UpdateAsync(tour);
        }
        await _tourRepository.SaveChangesAsync();

        return new PropertyTourResponse { PropertyId = propertyId, Title = request.Title, Rooms = request.Rooms };
    }
}

public class LandlordWorkspaceService : ILandlordWorkspaceService
{
    private readonly IPropertyRepository _propertyRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IReviewRepository _reviewRepository;
    private readonly IConfiguration _configuration;

    public LandlordWorkspaceService(
        IPropertyRepository propertyRepository,
        IBookingRepository bookingRepository,
        IRepository<User> userRepository,
        IReviewRepository reviewRepository,
        IConfiguration configuration)
    {
        _propertyRepository = propertyRepository;
        _bookingRepository = bookingRepository;
        _userRepository = userRepository;
        _reviewRepository = reviewRepository;
        _configuration = configuration;
    }

    /// <summary>Display stage for the reservations table, derived from status + dates.</summary>
    private static string StageFor(Booking b) => b.Status switch
    {
        BookingStatus.Cancelled => "Canceled",
        BookingStatus.Completed or BookingStatus.CheckedOut => "Complete",
        _ when b.CheckOutDate.Date <= DateTime.UtcNow.Date => "Complete",
        _ when b.CheckInDate.Date <= DateTime.UtcNow.Date => "Active",
        _ => "Upcoming"
    };

    public async Task<PagedResult<LandlordBookingResponse>> GetBookingsAsync(string landlordId, int page, int pageSize)
    {
        var (pageNum, size) = Paging.Clamp(page, pageSize);
        var properties = (await _propertyRepository.GetByUserIdAsync(landlordId))
            .ToDictionary(p => p.Id, p => p.Title);
        if (properties.Count == 0)
            return Paging.Result(Array.Empty<LandlordBookingResponse>(), 0, pageNum, size);

        var propertyIds = properties.Keys.ToList();
        var (bookings, totalCount) = await _bookingRepository.FindPageAsync(
            b => propertyIds.Contains(b.PropertyId),
            q => q.OrderByDescending(b => b.CheckInDate),
            pageNum, size);
        var tenantNames = await LoadTenantNamesAsync(bookings);

        var mapped = bookings
            .Select(b => new LandlordBookingResponse
            {
                BookingId = b.Id,
                PropertyId = b.PropertyId,
                Listing = properties.GetValueOrDefault(b.PropertyId),
                Guest = tenantNames.GetValueOrDefault(b.TenantId),
                CheckIn = b.CheckInDate,
                CheckOut = b.CheckOutDate,
                Nights = Math.Max(0, (b.CheckOutDate.Date - b.CheckInDate.Date).Days),
                Guests = b.Guests,
                Amount = b.TotalAmount,
                Status = b.Status,
                Stage = StageFor(b)
            })
            .ToList();

        return Paging.Result(mapped, totalCount, pageNum, size);
    }

    public async Task<ReservationDetailsResponse> GetReservationAsync(string bookingId, string landlordId)
    {
        var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId)
            ?? throw new NotFoundException("Reservation");

        // Only the landlord who owns the listing may see the reservation's details.
        if (booking.Property?.UserId != landlordId)
            throw new ForbiddenException("You do not own this listing");

        var nights = Math.Max(1, (booking.CheckOutDate.Date - booking.CheckInDate.Date).Days);

        // Earnings breakdown: the platform's management fee is a configurable percentage of the
        // gross booking revenue; the host receives the remainder.
        var feePercent = _configuration.GetValue("Platform:ManagementFeePercent", 20m);
        var managementFee = Math.Round(booking.TotalAmount * feePercent / 100m, 2);

        var guestReviews = (await _reviewRepository.GetByPropertyIdAsync(booking.PropertyId))
            .Where(r => r.ReviewerId == booking.TenantId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new GuestReviewItem { Rating = r.Rating, Comment = r.Comment, CreatedAt = r.CreatedAt })
            .ToList();

        return new ReservationDetailsResponse
        {
            BookingId = booking.Id,
            PropertyId = booking.PropertyId,
            Listing = booking.Property?.Title,
            CheckIn = booking.CheckInDate,
            CheckOut = booking.CheckOutDate,
            Nights = nights,
            Guests = booking.Guests,
            Status = booking.Status,
            Stage = StageFor(booking),
            GuestId = booking.TenantId,
            GuestName = booking.Tenant?.FullName,
            GuestTripNestId = booking.Tenant?.TripNestId,
            NightlyRate = Math.Round(booking.TotalAmount / nights, 2),
            NetRevenue = booking.TotalAmount,
            ManagementFeePercent = feePercent,
            ManagementFee = managementFee,
            OwnerPayout = booking.TotalAmount - managementFee,
            GuestReviews = guestReviews
        };
    }

    public async Task<PagedResult<LandlordTenantResponse>> GetTenantsAsync(string landlordId, int page, int pageSize)
    {
        var properties = (await _propertyRepository.GetByUserIdAsync(landlordId)).ToList();
        var propertyById = properties.ToDictionary(p => p.Id);
        if (properties.Count == 0)
            return Paging.Page(Array.Empty<LandlordTenantResponse>(), page, pageSize);

        var propertyIds = properties.Select(p => p.Id).ToList();
        var active = (await _bookingRepository.FindAsync(b => propertyIds.Contains(b.PropertyId)))
            .Where(b => b.Status != BookingStatus.Cancelled)
            .ToList();

        var tenantIds = active.Select(b => b.TenantId).Distinct().ToList();
        var tenants = (await _userRepository.FindAsync(u => tenantIds.Contains(u.Id)))
            .ToDictionary(u => u.Id);

        var now = DateTime.UtcNow;
        var mapped = active
            .GroupBy(b => b.TenantId)
            .Select(g =>
            {
                var latest = g.OrderByDescending(b => b.CheckOutDate).First();
                var property = propertyById.GetValueOrDefault(latest.PropertyId);
                var leaseEnd = g.Max(b => b.CheckOutDate);
                var standing = leaseEnd.Date < now.Date
                    ? "overdue"
                    : (leaseEnd.Date - now.Date).TotalDays <= 30 ? "ending-soon" : "current";
                var tenant = tenants.GetValueOrDefault(g.Key);

                return new LandlordTenantResponse
                {
                    TenantId = g.Key,
                    Name = tenant?.FullName,
                    Email = tenant?.Email,
                    Phone = tenant?.Phone,
                    Property = property?.Title,
                    Since = g.Min(b => b.CheckInDate),
                    LeaseEnd = leaseEnd,
                    MonthlyRent = property?.MonthlyRent ?? 0,
                    Standing = standing
                };
            })
            .ToList();

        return Paging.Page(mapped, page, pageSize);
    }

    private async Task<Dictionary<string, string>> LoadTenantNamesAsync(IReadOnlyList<Booking> bookings)
    {
        var ids = bookings.Select(b => b.TenantId).Distinct().ToList();
        return (await _userRepository.FindAsync(u => ids.Contains(u.Id)))
            .ToDictionary(u => u.Id, u => u.FullName);
    }
}
