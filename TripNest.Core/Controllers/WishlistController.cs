using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Models;
using TripNest.Core.Response;
using TripNest.Core.Extensions;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/wishlist")]
[Authorize]
[Produces("application/json")]
public class WishlistController : ControllerBase
{
    private readonly IRepository<WishlistItem> _wishlistRepository;
    private readonly IPropertyRepository _propertyRepository;
    private readonly ILogger<WishlistController> _logger;

    public WishlistController(
        IRepository<WishlistItem> wishlistRepository,
        IPropertyRepository propertyRepository,
        ILogger<WishlistController> logger)
    {
        _wishlistRepository = wishlistRepository;
        _propertyRepository = propertyRepository;
        _logger = logger;
    }

    [HttpGet("mine")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<object>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IEnumerable<object>>>> GetMyWishlist()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var mine = await _wishlistRepository.FindAsync(w => w.UserId == userId);
        var items = mine
            .Select(w => (object)new
            {
                w.Id,
                w.PropertyId,
                w.AddedAt
            })
            .ToList();

        return Ok(ApiResponse<IEnumerable<object>>.Ok("Wishlist retrieved", items));
    }

    [HttpPost("{propertyId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<object>>> AddToWishlist(string propertyId)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var property = await _propertyRepository.GetByIdAsync(propertyId);
        if (property is null)
            return NotFound(ApiResponse<object>.NotFound("Property"));

        var existing = (await _wishlistRepository.FindAsync(w => w.UserId == userId && w.PropertyId == propertyId))
            .FirstOrDefault();
        if (existing is not null)
            return Conflict(ApiResponse<object>.Conflict("WishlistItem"));

        var item = new WishlistItem
        {
            UserId = userId,
            PropertyId = propertyId
        };

        var created = await _wishlistRepository.AddAsync(item);
        await _wishlistRepository.SaveChangesAsync();

        var result = (object)new
        {
            created.Id,
            created.PropertyId,
            created.AddedAt
        };

        return Created($"api/wishlist/mine",
            ApiResponse<object>.Created("WishlistItem", result));
    }

    [HttpDelete("{propertyId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> RemoveFromWishlist(string propertyId)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var item = (await _wishlistRepository.FindAsync(w => w.UserId == userId && w.PropertyId == propertyId))
            .FirstOrDefault();
        if (item is null)
            return NotFound(ApiResponse<object>.NotFound("WishlistItem"));

        await _wishlistRepository.DeleteAsync(item);
        await _wishlistRepository.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok("Property removed from wishlist"));
    }
}
