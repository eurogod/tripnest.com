namespace TripNest.Core.Middleware;

/// <summary>
/// Adds baseline security response headers to every response. These are cheap defence-in-depth:
/// <list type="bullet">
/// <item><c>X-Content-Type-Options: nosniff</c> stops browsers MIME-sniffing user-uploaded media
/// served from <c>wwwroot/uploads</c> into an executable type.</item>
/// <item><c>X-Frame-Options: DENY</c> / <c>frame-ancestors 'none'</c> block clickjacking.</item>
/// <item><c>Referrer-Policy</c> avoids leaking URLs (which can carry ids/tokens) to third parties.</item>
/// </list>
/// A strict <c>Content-Security-Policy</c> is applied outside Development; in Development it is skipped
/// so the Swagger UI (which loads its own scripts/styles) keeps working.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _isDevelopment;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        _next = next;
        _isDevelopment = environment.IsDevelopment();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Apply just before the response is sent so the headers survive any downstream
        // Response.Clear() (e.g. the exception handler rewriting an error response).
        context.Response.OnStarting(state =>
        {
            var headers = ((HttpContext)state).Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["X-Permitted-Cross-Domain-Policies"] = "none";

            if (!_isDevelopment)
                headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";

            return Task.CompletedTask;
        }, context);

        await _next(context);
    }
}
