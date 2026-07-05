using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Marketplace;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/payments/methods")]
[Produces("application/json")]
[Authorize]
public class PaymentMethodsController : ControllerBase
{
    private readonly IPaymentMethodService _paymentMethodService;

    public PaymentMethodsController(IPaymentMethodService paymentMethodService) => _paymentMethodService = paymentMethodService;

    /// <summary>List the caller's saved payment methods (primary first).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<PaymentMethodResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<PaymentMethodResponse>>>> GetMine()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<List<PaymentMethodResponse>>.UnAuthorized());

        var methods = await _paymentMethodService.GetMineAsync(userId);
        return Ok(ApiResponse<List<PaymentMethodResponse>>.Ok("Payment methods retrieved", methods));
    }

    /// <summary>Save a new payment method.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PaymentMethodResponse>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<PaymentMethodResponse>>> Add([FromBody] CreatePaymentMethodRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<PaymentMethodResponse>.UnAuthorized());

        var method = await _paymentMethodService.AddAsync(request, userId);
        return Created($"api/payments/methods/{method.Id}", ApiResponse<PaymentMethodResponse>.Created("Payment method", method));
    }

    /// <summary>Mark a saved payment method as the primary one.</summary>
    [HttpPatch("{id}/primary")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> SetPrimary(string id)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        await _paymentMethodService.SetPrimaryAsync(id, userId);
        return Ok(ApiResponse<object>.Ok("Primary payment method updated"));
    }

    /// <summary>Remove a saved payment method.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string id)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        await _paymentMethodService.DeleteAsync(id, userId);
        return Ok(ApiResponse<object>.Ok("Payment method removed"));
    }
}
