using TripNest.Core.DTOs.Auth;
using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Services;

public interface IAuthService
{
    Task<User> RegisterAsync(RegisterRequest request);

    Task<LoginResponse> LoginAsync(LoginRequest request);

    /// <summary>Signs in (or provisions on first use) a user from a verified external identity
    /// (e.g. Google or Facebook), issuing tokens exactly like a password login.</summary>
    Task<LoginResponse> ExternalSignInAsync(string email, string fullName, bool emailVerified);

    /// <summary>Completes a phone-OTP login: checks the code sent by
    /// <see cref="IPhoneVerificationService.SendLoginOtpAsync"/> and issues tokens exactly like a
    /// password login. Throws on a wrong/expired code.</summary>
    Task<LoginResponse> PhoneLoginAsync(string phone, string code);

    Task<LoginResponse> RefreshTokenAsync(string refreshToken);

    Task ChangePasswordAsync(string userId, ChangePasswordRequest request);

    /// <summary>Revokes the user's refresh token so it can no longer be exchanged for access tokens.</summary>
    Task LogoutAsync(string userId);

    Task<(User? User, string? ResetToken)> ForgotPasswordAsync(string email);

    Task ResetPasswordAsync(string email, string resetToken, string newPassword);
}
