using Microsoft.AspNetCore.Http;
using TripNest.Core.DTOs.Claims;
using TripNest.Core.DTOs.Shared;

namespace TripNest.Core.Interfaces.Services;

public interface IDamageClaimService
{
    /// <summary>Landlord files the claim (one per booking, within Claims:FilingWindowDays of
    /// checkout, capped at Claims:MaxAmount) with photo evidence. The tenant is notified.</summary>
    Task<DamageClaimResponse> FileAsync(string landlordId, string bookingId, decimal amount, string description, IFormFileCollection? photos);

    Task<PagedResult<DamageClaimResponse>> GetMineAsync(string landlordId, int page, int pageSize);
    /// <summary>The claim on a booking — its landlord, tenant, or an admin.</summary>
    Task<DamageClaimResponse> GetForBookingAsync(string bookingId, string userId, bool isAdmin);
    /// <summary>The tenant attaches their (single) response.</summary>
    Task<DamageClaimResponse> RespondAsync(string claimId, string tenantId, string response);

    Task<PagedResult<DamageClaimResponse>> GetForReviewAsync(int page, int pageSize);
    /// <summary>Admin approves (optionally adjusting the amount) — pays the landlord immediately.</summary>
    Task<DamageClaimResponse> ApproveAsync(string claimId, decimal? approvedAmount, string? note);
    Task<DamageClaimResponse> RejectAsync(string claimId, string reason);
}
