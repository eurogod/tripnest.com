using System.ComponentModel.DataAnnotations;
using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Auth;

// Shape validation only ([ApiController] auto-400s with field-level errors before the service
// runs); business rules — password policy, E.164 phone, role restrictions — stay in AuthService.
public class RegisterRequest
{
    [StringLength(100, MinimumLength = 2)]
    public required string FullName { get; set; }

    [EmailAddress, StringLength(254)]
    public required string Email { get; set; }

    [StringLength(128)]
    public required string Password { get; set; }

    [StringLength(128)]
    public required string ConfirmPassword { get; set; }

    [StringLength(20)]
    public required string Phone { get; set; }

    public required UserRole Role { get; set; }
}
