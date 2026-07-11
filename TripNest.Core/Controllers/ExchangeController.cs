using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Marketplace;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/exchange")]
[Produces("application/json")]
[Authorize]
public class ExchangeController : ControllerBase
{
    private readonly IExchangeService _exchangeService;

    public ExchangeController(IExchangeService exchangeService) => _exchangeService = exchangeService;

    /// <summary>List Owner Exchange posts (pinned first, then newest).</summary>
    [HttpGet("posts")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ExchangePostResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<ExchangePostResponse>>>> GetPosts([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var posts = await _exchangeService.GetPostsAsync(page, pageSize);
        return Ok(ApiResponse<PagedResult<ExchangePostResponse>>.Ok("Posts retrieved", posts));
    }

    /// <summary>Create an Owner Exchange post.</summary>
    [HttpPost("posts")]
    [ProducesResponseType(typeof(ApiResponse<ExchangePostResponse>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<ExchangePostResponse>>> CreatePost([FromBody] CreateExchangePostRequest request)
    {
        var authorId = User.GetUserId();
        if (string.IsNullOrEmpty(authorId))
            return Unauthorized(ApiResponse<ExchangePostResponse>.UnAuthorized());

        var post = await _exchangeService.CreatePostAsync(request, authorId);
        return Created($"api/exchange/posts/{post.Id}", ApiResponse<ExchangePostResponse>.Created("Post", post));
    }

    /// <summary>List replies on a post.</summary>
    [HttpGet("posts/{postId}/replies")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ExchangeReplyResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<ExchangeReplyResponse>>>> GetReplies(string postId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var replies = await _exchangeService.GetRepliesAsync(postId, page, pageSize);
        return Ok(ApiResponse<PagedResult<ExchangeReplyResponse>>.Ok("Replies retrieved", replies));
    }

    /// <summary>Reply to a post.</summary>
    [HttpPost("posts/{postId}/replies")]
    [ProducesResponseType(typeof(ApiResponse<ExchangeReplyResponse>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<ExchangeReplyResponse>>> AddReply(string postId, [FromBody] CreateExchangeReplyRequest request)
    {
        var authorId = User.GetUserId();
        if (string.IsNullOrEmpty(authorId))
            return Unauthorized(ApiResponse<ExchangeReplyResponse>.UnAuthorized());

        var reply = await _exchangeService.AddReplyAsync(postId, request, authorId);
        return Created($"api/exchange/posts/{postId}/replies/{reply.Id}", ApiResponse<ExchangeReplyResponse>.Created("Reply", reply));
    }
}
