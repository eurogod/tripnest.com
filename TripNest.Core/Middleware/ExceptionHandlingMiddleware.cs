using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TripNest.Core.Exceptions;
using TripNest.Core.Response;

namespace TripNest.Core.Middleware;

/// <summary>
/// Translates unhandled exceptions into a consistent ApiResponse with the right status code,
/// so domain failures surface as 400/403/404/409 instead of an opaque 500.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await WriteResponseAsync(context, ex);
        }
    }

    private async Task WriteResponseAsync(HttpContext context, Exception ex)
    {
        var (statusCode, message) = ex switch
        {
            DomainException domain => (domain.StatusCode, ex.Message),
            // Treat a plain InvalidOperationException as a 400 business-rule failure (legacy convention).
            InvalidOperationException => (400, ex.Message),
            // Legacy ownership/permission guards throw these; surface as 403/400 rather than an opaque
            // 500 so controllers relying on the middleware (the lean pattern) get correct status codes.
            UnauthorizedAccessException => (403, ex.Message),
            ArgumentException => (400, ex.Message),
            DbUpdateConcurrencyException => (409, "The resource was modified by another request. Please retry."),
            _ => (500, "An error occurred")
        };

        if (statusCode >= 500)
            _logger.LogError(ex, "Unhandled exception");
        else
            _logger.LogWarning("{StatusCode}: {Message}", statusCode, ex.Message);

        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Response already started; cannot write error body");
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var body = new ApiResponse<object>(message, statusCode, null);
        await context.Response.WriteAsync(JsonSerializer.Serialize(body,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
