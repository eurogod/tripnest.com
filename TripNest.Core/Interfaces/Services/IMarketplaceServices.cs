using TripNest.Core.DTOs.Marketplace;

namespace TripNest.Core.Interfaces.Services;

public interface IPricingService
{
    Task<PricingSettingsResponse> GetAsync(string propertyId, string landlordId);
    Task<PricingSettingsResponse> UpdateAsync(string propertyId, UpdatePricingSettingsRequest request, string landlordId);
}

public interface ICalendarService
{
    Task<CalendarMonthResponse> GetMonthAsync(string propertyId, int year, int month, string landlordId);
}

public interface IInquiryService
{
    Task<InquiryResponse> CreateAsync(CreateInquiryRequest request, string? guestUserId, string? guestNameFallback);
    Task<List<InquiryResponse>> GetForLandlordAsync(string landlordId);
    Task<InquiryResponse> UpdateStatusAsync(string inquiryId, string status, string landlordId);
}

public interface IPaymentMethodService
{
    Task<List<PaymentMethodResponse>> GetMineAsync(string userId);
    Task<PaymentMethodResponse> AddAsync(CreatePaymentMethodRequest request, string userId);
    Task SetPrimaryAsync(string id, string userId);
    Task DeleteAsync(string id, string userId);
}

public interface IExchangeService
{
    Task<List<ExchangePostResponse>> GetPostsAsync();
    Task<ExchangePostResponse> CreatePostAsync(CreateExchangePostRequest request, string authorId);
    Task<List<ExchangeReplyResponse>> GetRepliesAsync(string postId);
    Task<ExchangeReplyResponse> AddReplyAsync(string postId, CreateExchangeReplyRequest request, string authorId);
}

public interface IHostTaskService
{
    Task<List<HostTaskResponse>> GetMineAsync(string landlordId);
    Task<HostTaskResponse> CreateAsync(CreateHostTaskRequest request, string landlordId);
    Task<HostTaskResponse> UpdateAsync(string id, UpdateHostTaskRequest request, string landlordId);
    Task DeleteAsync(string id, string landlordId);
}

public interface ITeamService
{
    Task<List<TeamMemberResponse>> GetMineAsync(string landlordId);
    Task<TeamMemberResponse> InviteAsync(InviteTeamMemberRequest request, string landlordId);
    Task<TeamMemberResponse> UpdateAsync(string id, UpdateTeamMemberRequest request, string landlordId);
    Task RemoveAsync(string id, string landlordId);
}

public interface IResourceService
{
    Task<List<ResourceResponse>> GetAllAsync();
    Task<ResourceResponse> CreateAsync(CreateResourceRequest request);
}

public interface IStatementService
{
    Task<List<StatementResponse>> GetForLandlordAsync(string landlordId);
}

public interface ITourService
{
    Task<PropertyTourResponse?> GetAsync(string propertyId);
    Task<PropertyTourResponse> UpsertAsync(string propertyId, UpsertPropertyTourRequest request, string landlordId);
}

public interface ILandlordWorkspaceService
{
    Task<List<LandlordBookingResponse>> GetBookingsAsync(string landlordId);
    Task<List<LandlordTenantResponse>> GetTenantsAsync(string landlordId);
}
