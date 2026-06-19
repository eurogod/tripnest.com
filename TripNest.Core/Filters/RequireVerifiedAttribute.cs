using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Response;

namespace TripNest.Core.Filters;

/// <summary>
/// Blocks role-specific actions for users whose identity is not yet verified.
/// Verification is compulsory for Landlord/Agent/Caretaker — they may log in, view dashboards,
/// edit their profile and complete verification, but their core actions return 403 until
/// <c>IsVerified</c> is true. Tenants, Guests and Admins are unaffected.
///
/// The verified flag is read from the database (not a JWT claim) because it flips
/// asynchronously after the background processor resolves a verification, so a token issued
/// at login would be stale.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class RequireVerifiedAttribute : Attribute, IAsyncAuthorizationFilter
{
    private static readonly string[] GatedRoles = { "Landlord", "Agent", "Caretaker" };

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var principal = context.HttpContext.User;

        // Only gated roles are subject to the verification requirement.
        if (!GatedRoles.Any(principal.IsInRole))
            return;

        var userId = principal.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            context.Result = new ObjectResult(ApiResponse<object>.UnAuthorized()) { StatusCode = 401 };
            return;
        }

        var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
        var account = await userRepository.GetByIdAsync(userId);

        if (account is null || !account.IsVerified)
        {
            context.Result = new ObjectResult(
                ApiResponse<object>.Forbidden("Identity verification is required before you can perform this action."))
            {
                StatusCode = 403
            };
        }
    }
}
