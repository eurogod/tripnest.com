using Microsoft.AspNetCore.Http;
using TripNest.Core.DTOs.Claims;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Enums;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class DamageClaimService : IDamageClaimService
{
    private readonly IRepository<DamageClaim> _claimRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IPropertyRepository _propertyRepository;
    private readonly IPayoutService _payoutService;
    private readonly INotificationService _notificationService;
    private readonly IFileStorage _fileStorage;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DamageClaimService> _logger;

    public DamageClaimService(
        IRepository<DamageClaim> claimRepository,
        IBookingRepository bookingRepository,
        IPropertyRepository propertyRepository,
        IPayoutService payoutService,
        INotificationService notificationService,
        IFileStorage fileStorage,
        IConfiguration configuration,
        ILogger<DamageClaimService> logger)
    {
        _claimRepository = claimRepository;
        _bookingRepository = bookingRepository;
        _propertyRepository = propertyRepository;
        _payoutService = payoutService;
        _notificationService = notificationService;
        _fileStorage = fileStorage;
        _configuration = configuration;
        _logger = logger;
    }

    private int FilingWindowDays => _configuration.GetValue("Claims:FilingWindowDays", 14);
    private decimal MaxAmount => _configuration.GetValue("Claims:MaxAmount", 5000m);

    public async Task<DamageClaimResponse> FileAsync(
        string landlordId, string bookingId, decimal amount, string description, IFormFileCollection? photos)
    {
        var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId)
            ?? throw new NotFoundException("Booking");

        var property = booking.Property ?? await _propertyRepository.GetByIdAsync(booking.PropertyId);
        if (property?.UserId != landlordId)
            throw new ForbiddenException("Only the property's landlord can file a claim on this booking");

        // A claim needs a stay that actually happened: the guest checked in, and we're within
        // the filing window after checkout — late claims are how disputes go stale.
        if (booking.Status is not (BookingStatus.CheckedIn or BookingStatus.CheckedOut or BookingStatus.Completed) &&
            !(booking.Status == BookingStatus.Confirmed && booking.CheckOutDate <= DateTime.UtcNow))
            throw new ValidationException("A claim can only be filed for a stay that has taken place");
        if (DateTime.UtcNow > booking.CheckOutDate.AddDays(FilingWindowDays))
            throw new ValidationException($"Claims must be filed within {FilingWindowDays} days of checkout");

        if (amount <= 0 || amount > MaxAmount)
            throw new ValidationException($"The claimed amount must be between 0 and {MaxAmount:0.00}");
        if (string.IsNullOrWhiteSpace(description))
            throw new ValidationException("Describe the damage");

        if ((await _claimRepository.FindAsync(c => c.BookingId == bookingId)).Any())
            throw new ConflictException("A claim already exists for this booking");

        // Evidence photos through the validated storage path (type/size checked there).
        var paths = new List<string>();
        if (photos is not null)
            foreach (var photo in photos)
                paths.Add(await _fileStorage.SaveAsync($"claims/{bookingId}", photo, UploadKind.Image));

        var claim = new DamageClaim
        {
            BookingId = bookingId,
            LandlordId = landlordId,
            TenantId = booking.TenantId,
            Amount = amount,
            Description = description.Trim(),
            PhotoPaths = paths.Count > 0 ? string.Join(',', paths) : null
        };
        await _claimRepository.AddAsync(claim);
        await _claimRepository.SaveChangesAsync();

        await _notificationService.CreateAsync(
            booking.TenantId,
            "A damage claim was filed on your stay",
            $"The host claims GHS {amount:0.00} for damage. You can add your response before an admin reviews it.",
            claim.Id, "DamageClaim");

        _logger.LogInformation("Damage claim {ClaimId} filed on booking {BookingId} for {Amount}", claim.Id, bookingId, amount);
        return Map(claim);
    }

    public async Task<PagedResult<DamageClaimResponse>> GetMineAsync(string landlordId, int page, int pageSize)
    {
        var claims = (await _claimRepository.FindAsync(c => c.LandlordId == landlordId))
            .OrderByDescending(c => c.CreatedAt).Select(Map).ToList();
        return Paging.Page(claims, page, pageSize);
    }

    public async Task<DamageClaimResponse> GetForBookingAsync(string bookingId, string userId, bool isAdmin)
    {
        var claim = (await _claimRepository.FindAsync(c => c.BookingId == bookingId)).FirstOrDefault()
            ?? throw new NotFoundException("Damage claim");
        if (!isAdmin && claim.LandlordId != userId && claim.TenantId != userId)
            throw new ForbiddenException("You are not part of this claim");
        return Map(claim);
    }

    public async Task<DamageClaimResponse> RespondAsync(string claimId, string tenantId, string response)
    {
        var claim = await _claimRepository.GetByIdAsync(claimId)
            ?? throw new NotFoundException("Damage claim");
        if (claim.TenantId != tenantId)
            throw new ForbiddenException("Only the stay's tenant can respond to this claim");
        if (claim.Status != DamageClaimStatus.Submitted)
            throw new ValidationException("This claim has already been decided");
        if (!string.IsNullOrEmpty(claim.TenantResponse))
            throw new ConflictException("You have already responded to this claim");
        if (string.IsNullOrWhiteSpace(response))
            throw new ValidationException("A response is required");

        claim.TenantResponse = response.Trim();
        await _claimRepository.UpdateAsync(claim);
        await _claimRepository.SaveChangesAsync();

        await _notificationService.CreateAsync(
            claim.LandlordId,
            "The tenant responded to your damage claim",
            "An admin will review both sides and decide.",
            claim.Id, "DamageClaim");
        return Map(claim);
    }

    public async Task<PagedResult<DamageClaimResponse>> GetForReviewAsync(int page, int pageSize)
    {
        var claims = (await _claimRepository.FindAsync(c => c.Status == DamageClaimStatus.Submitted))
            .OrderBy(c => c.CreatedAt).Select(Map).ToList();
        return Paging.Page(claims, page, pageSize);
    }

    public async Task<DamageClaimResponse> ApproveAsync(string claimId, decimal? approvedAmount, string? note)
    {
        var claim = await LoadSubmittedAsync(claimId);
        var amount = approvedAmount ?? claim.Amount;
        if (amount <= 0 || amount > claim.Amount)
            throw new ValidationException("The approved amount must be positive and not exceed the claimed amount");

        claim.Status = DamageClaimStatus.Approved;
        claim.ApprovedAmount = amount;
        claim.ResolutionNote = note?.Trim();
        claim.ResolvedAt = DateTime.UtcNow;
        await _claimRepository.UpdateAsync(claim);
        await _claimRepository.SaveChangesAsync();

        // The differentiator: approval pays immediately through the standard transfer machinery
        // (fee-free — damage protection is a make-whole payment, not revenue).
        await _payoutService.CreateForApprovedClaimAsync(claim);

        await _notificationService.CreateAsync(claim.LandlordId, "Damage claim approved",
            $"GHS {amount:0.00} is being paid out to your payout account now.", claim.Id, "DamageClaim");
        await _notificationService.CreateAsync(claim.TenantId, "Damage claim decided",
            $"The claim on your stay was approved for GHS {amount:0.00}.", claim.Id, "DamageClaim");

        _logger.LogInformation("Damage claim {ClaimId} approved for {Amount}", claimId, amount);
        return Map(claim);
    }

    public async Task<DamageClaimResponse> RejectAsync(string claimId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ValidationException("A rejection reason is required");

        var claim = await LoadSubmittedAsync(claimId);
        claim.Status = DamageClaimStatus.Rejected;
        claim.ResolutionNote = reason.Trim();
        claim.ResolvedAt = DateTime.UtcNow;
        await _claimRepository.UpdateAsync(claim);
        await _claimRepository.SaveChangesAsync();

        await _notificationService.CreateAsync(claim.LandlordId, "Damage claim rejected", reason.Trim(), claim.Id, "DamageClaim");
        await _notificationService.CreateAsync(claim.TenantId, "Damage claim decided",
            "The claim on your stay was rejected.", claim.Id, "DamageClaim");
        return Map(claim);
    }

    private async Task<DamageClaim> LoadSubmittedAsync(string claimId)
    {
        var claim = await _claimRepository.GetByIdAsync(claimId)
            ?? throw new NotFoundException("Damage claim");
        if (claim.Status != DamageClaimStatus.Submitted)
            throw new ValidationException("This claim has already been decided");
        return claim;
    }

    private static DamageClaimResponse Map(DamageClaim c) => new()
    {
        ClaimId = c.Id,
        BookingId = c.BookingId,
        LandlordId = c.LandlordId,
        TenantId = c.TenantId,
        Amount = c.Amount,
        ApprovedAmount = c.ApprovedAmount,
        Description = c.Description,
        PhotoPaths = string.IsNullOrEmpty(c.PhotoPaths) ? new List<string>() : c.PhotoPaths.Split(',').ToList(),
        Status = c.Status,
        TenantResponse = c.TenantResponse,
        ResolutionNote = c.ResolutionNote,
        CreatedAt = c.CreatedAt,
        ResolvedAt = c.ResolvedAt
    };
}
