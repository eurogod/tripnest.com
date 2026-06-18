using TripNest.Core.DTOs.Auth;
using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Services;

public interface IAuthService
{
    Task<User> RegisterAsync(RegisterRequest request);

    Task<LoginResponse> LoginAsync(LoginRequest request);

    Task<LoginResponse> RefreshTokenAsync(string refreshToken);

    Task ChangePasswordAsync(string userId, ChangePasswordRequest request);

    Task<(User User, string ResetToken)> ForgotPasswordAsync(string email);

    Task ResetPasswordAsync(string email, string resetToken, string newPassword);
}
