using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Marketplace;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/inquiries")]
[Produces("application/json")]
[Authorize]
public class InquiriesController : ControllerBase
{
    private readonly IInquiryService _inquiryService;

    public InquiriesController(IInquiryService inquiryService) => _inquiryService = inquiryService;

    /// <summary>Send a pre-booking enquiry to a listing's landlord.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<InquiryResponse>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<InquiryResponse>>> Create([FromBody] CreateInquiryRequest request)
    {
        var guestUserId = User.GetUserId();
        if (string.IsNullOrEmpty(guestUserId))
            return Unauthorized(ApiResponse<InquiryResponse>.UnAuthorized());

        var inquiry = await _inquiryService.CreateAsync(request, guestUserId, User.Identity?.Name);
        return Created($"api/inquiries/{inquiry.InquiryId}", ApiResponse<InquiryResponse>.Created("Inquiry", inquiry));
    }
}
