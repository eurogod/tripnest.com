using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Safety;
using TripNest.Core.Enums;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SafetyController : ControllerBase
{
    private readonly ISafetyCheckInRepository _checkInRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPhoneNumberValidator _phoneValidator;
    private readonly ISmsSender _smsSender;
    private readonly IEmailSender _emailSender;
    private readonly INotificationService _notificationService;
    private readonly ILogger<SafetyController> _logger;

    public SafetyController(
        ISafetyCheckInRepository checkInRepository,
        IBookingRepository bookingRepository,
        IUserRepository userRepository,
        IPhoneNumberValidator phoneValidator,
        ISmsSender smsSender,
        IEmailSender emailSender,
        INotificationService notificationService,
        ILogger<SafetyController> logger)
    {
        _checkInRepository = checkInRepository;
        _bookingRepository = bookingRepository;
        _userRepository = userRepository;
        _phoneValidator = phoneValidator;
        _smsSender = smsSender;
        _emailSender = emailSender;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>Gets the user's saved trusted contact used for safe-arrival check-ins.</summary>
    [HttpGet("contact")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<TrustedContactResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TrustedContactResponse>>> GetTrustedContact()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return NotFound(ApiResponse<object>.NotFound("User"));

        return Ok(ApiResponse<TrustedContactResponse>.Ok("Trusted contact retrieved", new TrustedContactResponse
        {
            Name = user.TrustedContactName,
            Phone = user.TrustedContactPhone,
            Email = user.TrustedContactEmail
        }));
    }

    /// <summary>Saves/updates the user's trusted contact. The phone is normalised to E.164.</summary>
    [HttpPut("contact")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<TrustedContactResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TrustedContactResponse>>> UpdateTrustedContact([FromBody] TrustedContactRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return NotFound(ApiResponse<object>.NotFound("User"));

        string? normalizedPhone = null;
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            normalizedPhone = _phoneValidator.Normalize(request.Phone);
            if (normalizedPhone == null)
                return BadRequest(ApiResponse<object>.BadRequest("Please provide a valid contact phone number"));
        }

        user.TrustedContactName = request.Name;
        user.TrustedContactPhone = normalizedPhone;
        user.TrustedContactEmail = request.Email;
        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        return Ok(ApiResponse<TrustedContactResponse>.Ok("Trusted contact saved", new TrustedContactResponse
        {
            Name = user.TrustedContactName,
            Phone = user.TrustedContactPhone,
            Email = user.TrustedContactEmail
        }));
    }

    [HttpPost("checkin")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SafetyCheckInResponse>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<SafetyCheckInResponse>>> CheckIn([FromBody] SafetyCheckInRequest request)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var booking = await _bookingRepository.GetByIdAsync(request.BookingId);
            if (booking == null)
                return BadRequest(ApiResponse<object>.BadRequest("Booking not found"));

            var user = await _userRepository.GetByIdAsync(userId);

            // Resolve the contact to notify: per-request override first, else the saved trusted contact.
            var contactPhoneRaw = !string.IsNullOrWhiteSpace(request.ContactPhone)
                ? request.ContactPhone : user?.TrustedContactPhone;
            var contactPhone = string.IsNullOrWhiteSpace(contactPhoneRaw)
                ? null : (_phoneValidator.Normalize(contactPhoneRaw) ?? contactPhoneRaw);
            var contactEmail = !string.IsNullOrWhiteSpace(request.ContactEmail)
                ? request.ContactEmail : user?.TrustedContactEmail;

            // Consent gate: attach/persist coordinates only when the user explicitly consented
            // AND coordinates were supplied. The app is responsible for asking first.
            string? mapsLink = null;
            var shareLocation = request.ShareLocation
                && request.Latitude is double && request.Longitude is double;
            if (shareLocation)
                mapsLink = $"https://maps.google.com/?q={request.Latitude},{request.Longitude}";

            var checkIn = await _checkInRepository.GetByBookingIdAsync(request.BookingId);
            if (checkIn == null)
            {
                checkIn = new SafetyCheckIn
                {
                    BookingId = request.BookingId,
                    EmergencyContactPhone = contactPhone,
                    EmergencyContactEmail = contactEmail,
                    CheckedInAt = DateTime.UtcNow
                };
                ApplyLocation(checkIn, shareLocation, request);
                await _checkInRepository.AddAsync(checkIn);
            }
            else
            {
                checkIn.CheckedInAt = DateTime.UtcNow;
                if (contactPhone != null) checkIn.EmergencyContactPhone = contactPhone;
                if (contactEmail != null) checkIn.EmergencyContactEmail = contactEmail;
                ApplyLocation(checkIn, shareLocation, request);
                await _checkInRepository.UpdateAsync(checkIn);
            }

            await _checkInRepository.SaveChangesAsync();

            // Notify the chosen contact that the traveller arrived safely (best-effort; the senders
            // swallow failures and return false, so a delivery problem never fails the check-in).
            var contactNotified = false;
            if (contactPhone != null || contactEmail != null)
            {
                var name = string.IsNullOrWhiteSpace(user?.FullName) ? "Your TripNest contact" : user!.FullName;
                var text = $"{name} has checked in safely via TripNest.";
                if (mapsLink != null) text += $" Location: {mapsLink}";

                if (contactPhone != null)
                    contactNotified |= await _smsSender.SendSmsAsync(contactPhone, text);
                if (contactEmail != null)
                    contactNotified |= await _emailSender.SendAsync(contactEmail, "TripNest safe-arrival check-in", $"<p>{text}</p>");
            }

            return Created($"api/safety/checkin/{checkIn.Id}", ApiResponse<SafetyCheckInResponse>.Created("Check-in", new SafetyCheckInResponse
            {
                CheckInId = checkIn.Id,
                BookingId = checkIn.BookingId,
                IsCheckedIn = true,
                CheckedInAt = checkIn.CheckedInAt,
                ContactNotified = contactNotified,
                LocationShared = checkIn.LocationShared
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing check-in");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    // Persist coordinates only with consent; otherwise leave them cleared.
    private static void ApplyLocation(SafetyCheckIn checkIn, bool shareLocation, SafetyCheckInRequest request)
    {
        checkIn.LocationShared = shareLocation;
        checkIn.Latitude = shareLocation ? request.Latitude : null;
        checkIn.Longitude = shareLocation ? request.Longitude : null;
    }

    [HttpPost("alert")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> SendEmergencyAlert([FromBody] SafetyCheckInRequest request)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            const string alertText = "Emergency alert from TripNest. Immediate assistance may be needed.";

            // Auditable emergency notification to the tenant — bypasses their opt-out (SMS + email)
            // and records IsEmergencyOverride = true. This is the one path that ignores preferences.
            await _notificationService.NotifyAsync(userId, NotificationType.SafetyAlert,
                "Emergency alert", alertText, isEmergency: true);

            // Also alert the trusted contact directly (they may not be a TripNest user, so there's
            // no in-app record / preference to attach to). Prefer the contact recorded on the
            // check-in; fall back to the user's saved trusted contact.
            var checkIn = await _checkInRepository.GetByBookingIdAsync(request.BookingId);
            var user = await _userRepository.GetByIdAsync(userId);

            var alertPhone = checkIn?.EmergencyContactPhone ?? user?.TrustedContactPhone;
            var alertEmail = checkIn?.EmergencyContactEmail ?? user?.TrustedContactEmail;

            if (!string.IsNullOrWhiteSpace(alertPhone))
                await _smsSender.SendSmsAsync(alertPhone, alertText);
            if (!string.IsNullOrWhiteSpace(alertEmail))
                await _emailSender.SendAsync(alertEmail, "Emergency alert", $"<p>{alertText}</p>");

            if (checkIn != null)
            {
                checkIn.AlertSentAt = DateTime.UtcNow;
                await _checkInRepository.UpdateAsync(checkIn);
                await _checkInRepository.SaveChangesAsync();
            }

            return Ok(ApiResponse<object>.Ok("Emergency alert sent", new { }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending emergency alert");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
