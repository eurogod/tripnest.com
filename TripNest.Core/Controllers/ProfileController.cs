using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Auth;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.Extensions;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IPhoneNumberValidator _phoneValidator;
    private readonly IVerificationRepository _verificationRepository;
    private readonly IRepository<Models.Agreement> _agreementRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        IUserRepository userRepository,
        IFileStorage fileStorage,
        IPhoneNumberValidator phoneValidator,
        IVerificationRepository verificationRepository,
        IRepository<Models.Agreement> agreementRepository,
        IConfiguration configuration,
        ILogger<ProfileController> logger)
    {
        _userRepository = userRepository;
        _fileStorage = fileStorage;
        _phoneValidator = phoneValidator;
        _verificationRepository = verificationRepository;
        _agreementRepository = agreementRepository;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>Downloads the verified user's TripNest ID card as a PDF. Requires a verified identity.</summary>
    [HttpGet("id-card")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetIdCard()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return NotFound(ApiResponse<object>.NotFound("User"));

        if (!user.IsVerified || string.IsNullOrWhiteSpace(user.TripNestId))
            return BadRequest(ApiResponse<object>.BadRequest(
                "Your identity isn't verified yet, so a TripNest ID card can't be issued."));

        var pdf = Pdf.IdCardPdf.Render(user, await TryReadPhotoAsync(user.ProfilePhotoPath));
        return File(pdf, "application/pdf", $"tripnest-id-{user.TripNestId}.pdf");
    }

    // Best-effort load of the profile photo for embedding; placeholder initials are used if missing.
    // Reads through IFileStorage so the same stored path works under both local disk and Azure Blob,
    // and so the read is constrained to the uploads area (no arbitrary-path reads).
    private async Task<byte[]?> TryReadPhotoAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        try
        {
            await using var stream = await _fileStorage.OpenReadAsync(path);
            if (stream is null)
                return null;

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            return buffer.ToArray();
        }
        catch (Exceptions.ValidationException)
        {
            // Path outside the uploads area — treat as no photo rather than surfacing an error.
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load profile photo for ID card");
            return null;
        }
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetProfile()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return NotFound(ApiResponse<object>.NotFound("User"));

        var profile = new
        {
            user.Id,
            user.FullName,
            user.Email,
            user.Phone,
            user.Role,
            user.IsVerified,
            user.EmailVerified,
            user.PhoneVerified,
            user.TripNestId,
            user.ProfilePhotoPath,
            user.Username,
            user.Bio,
            user.PreferredLanguage
        };

        return Ok(ApiResponse<object>.Ok("Profile retrieved", profile));
    }

    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return NotFound(ApiResponse<object>.NotFound("User"));

        // FullName is the identity name printed on the verified TripNest ID card and rental
        // agreements. Once identity is verified it's bound to the Ghana Card and must not change —
        // otherwise a user could verify as one person and then issue documents under another name.
        // Before verification it's self-asserted and freely editable.
        if (request.FullName is not null && request.FullName != user.FullName)
        {
            if (user.IsVerified)
                throw new InvalidOperationException(
                    "Your name is locked to your verified Ghana Card identity and can't be changed.");
            user.FullName = request.FullName;
        }

        user.Bio = request.Bio ?? user.Bio;
        if (request.PreferredLanguage is not null)
        {
            // The enum deserializes from any integer by default; reject undefined values (e.g. 440)
            // instead of silently storing garbage that falls back to English downstream.
            if (!Enum.IsDefined(typeof(TripNest.Core.Enums.Language), request.PreferredLanguage.Value))
                throw new InvalidOperationException("Invalid language selection.");
            user.PreferredLanguage = request.PreferredLanguage.Value;
        }
        if (request.Username is not null)
        {
            var username = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim();

            // A handle identifies one account: refuse any username another user already holds
            // (case-insensitively, so "Kwame" can't impersonate "kwame"). The unique index is
            // the backstop; this check gives a clean 400 instead of a database error.
            if (username is not null)
            {
                var lowered = username.ToLowerInvariant();
                var taken = (await _userRepository.FindAsync(u =>
                        u.Id != userId && u.Username != null && u.Username.ToLower() == lowered))
                    .Any();
                if (taken)
                    throw new InvalidOperationException("That username is already taken");
            }

            user.Username = username;
        }

        // Normalise the phone to E.164 (same as registration) so a profile edit can't store an
        // invalid number that later breaks SMS/OTP delivery.
        if (request.Phone is not null)
        {
            user.Phone = _phoneValidator.Normalize(request.Phone)
                ?? throw new InvalidOperationException("Please provide a valid phone number");
        }

        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok("Profile updated", new { }));
    }

    [HttpPost("photo")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> UploadProfilePhoto(IFormFile photo)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        if (photo == null || photo.Length == 0)
            return BadRequest(ApiResponse<object>.BadRequest("Photo file is required"));

        // Storage validates type + size and returns a servable path/URL (local disk or Azure Blob).
        var photoPath = await _fileStorage.SaveAsync("profiles", photo, UploadKind.Image);

        var user = await _userRepository.GetByIdAsync(userId);
        if (user != null)
        {
            user.ProfilePhotoPath = photoPath;
            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveChangesAsync();
        }

        return Ok(ApiResponse<object>.Ok("Profile photo uploaded", new { photoPath }));
    }

    // ------------------------------------------------------------- signature

    /// <summary>
    /// The caller's signature status: whether one is on file, when it was last set, and from when
    /// it becomes editable again. The image itself is never exposed via API — it only appears
    /// inside agreement PDFs the caller is a party to.
    /// </summary>
    [HttpGet("signature")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetSignatureInfo()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return NotFound(ApiResponse<object>.NotFound("User"));

        return Ok(ApiResponse<object>.Ok("Signature status", new
        {
            hasSignature = !string.IsNullOrEmpty(user.SignatureImagePath),
            updatedAt = user.SignatureUpdatedAt,
            editableFrom = user.SignatureUpdatedAt?.AddDays(SignatureEditCooldownDays)
        }));
    }

    /// <summary>
    /// Sets the caller's signature image. The FIRST upload is free; any later change is guarded:
    /// it requires the account password, the Ghana Card number when the identity is verified, and
    /// at least the cooldown period (default 30 days) since the last change — a signature is what
    /// lands on contracts, so replacing it must be deliberate and rare.
    /// </summary>
    [HttpPost("signature")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<object>>> UploadSignature(
        IFormFile signature, [FromForm] string? password = null, [FromForm] string? ghanaCardNumber = null)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());
        if (signature == null || signature.Length == 0)
            return BadRequest(ApiResponse<object>.BadRequest("A signature image is required"));

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return NotFound(ApiResponse<object>.NotFound("User"));

        // Replacing an existing (or previously set) signature is the guarded path.
        if (user.SignatureUpdatedAt.HasValue)
        {
            var editableFrom = user.SignatureUpdatedAt.Value.AddDays(SignatureEditCooldownDays);
            if (DateTime.UtcNow < editableFrom)
                return BadRequest(ApiResponse<object>.BadRequest(
                    $"Your signature can be changed again from {editableFrom:yyyy-MM-dd}. It was last set on {user.SignatureUpdatedAt:yyyy-MM-dd}."));

            var reAuthError = await CheckSignatureReAuthAsync(user, password, ghanaCardNumber);
            if (reAuthError is not null)
                return StatusCode(403, ApiResponse<object>.Forbidden(reAuthError));
        }

        var oldPath = user.SignatureImagePath;
        user.SignatureImagePath = await _fileStorage.SaveAsync($"signatures/{userId}", signature, UploadKind.Image);
        user.SignatureUpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        // Best-effort cleanup of the replaced image — but agreements snapshot the path they were
        // signed with, so a path any agreement references must stay servable for its PDF.
        if (!string.IsNullOrEmpty(oldPath) && !await AnyAgreementReferencesAsync(oldPath))
            try { await _fileStorage.DeleteAsync(oldPath); } catch { /* orphan cleanup only */ }

        _logger.LogInformation("Signature image set for user {UserId}", userId);
        return Ok(ApiResponse<object>.Ok("Signature saved", new
        {
            updatedAt = user.SignatureUpdatedAt,
            editableFrom = user.SignatureUpdatedAt.Value.AddDays(SignatureEditCooldownDays)
        }));
    }

    private int SignatureEditCooldownDays =>
        _configuration.GetValue("Profile:SignatureEditCooldownDays", 30);

    /// <summary>Re-auth for signature changes: the account password always; the Ghana Card number
    /// too when the account is identity-verified and a card is on file. Returns the refusal
    /// message, or null when authenticated.</summary>
    private async Task<string?> CheckSignatureReAuthAsync(Models.User user, string? password, string? ghanaCardNumber)
    {
        if (string.IsNullOrEmpty(password) || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return "Changing your signature requires your account password.";

        if (!user.IsVerified)
            return null;

        var cardOnFile = (await _verificationRepository.FindAsync(v =>
                v.UserId == user.Id && v.Status == Enums.VerificationStatus.Verified))
            .OrderByDescending(v => v.SubmittedAt)
            .FirstOrDefault()?.GhanaCardNumber;
        if (string.IsNullOrEmpty(cardOnFile))
            return null; // verified flag without a card record (seed/admin path) — password suffices

        var provided = (ghanaCardNumber ?? "").Trim().Replace(" ", "");
        if (!string.Equals(provided, cardOnFile.Trim().Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
            return "Changing your signature requires the Ghana Card number on your verified identity.";

        return null;
    }

    private async Task<bool> AnyAgreementReferencesAsync(string path) =>
        (await _agreementRepository.FindAsync(a =>
            a.TenantSignatureImagePath == path || a.LandlordSignatureImagePath == path)).Any();
}
