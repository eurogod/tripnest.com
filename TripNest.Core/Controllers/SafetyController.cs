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
    private readonly ISmsSender _smsSender;
    private readonly IEmailSender _emailSender;
    private readonly INotificationService _notificationService;
    private readonly ILogger<SafetyController> _logger;

    public SafetyController(
        ISafetyCheckInRepository checkInRepository,
        IBookingRepository bookingRepository,
        ISmsSender smsSender,
        IEmailSender emailSender,
        INotificationService notificationService,
        ILogger<SafetyController> logger)
    {
        _checkInRepository = checkInRepository;
        _bookingRepository = bookingRepository;
        _smsSender = smsSender;
        _emailSender = emailSender;
        _notificationService = notificationService;
        _logger = logger;
    }

    [HttpPost("checkin")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SafetyCheckInResponse>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<SafetyCheckInResponse>>> CheckIn([FromBody] SafetyCheckInRequest request)
    {
        try
        {
            var booking = await _bookingRepository.GetByIdAsync(request.BookingId);
            if (booking == null)
                return BadRequest(ApiResponse<object>.BadRequest("Booking not found"));

            var checkIn = await _checkInRepository.GetByBookingIdAsync(request.BookingId);
            if (checkIn == null)
            {
                checkIn = new SafetyCheckIn
                {
                    BookingId = request.BookingId,
                    EmergencyContactPhone = request.EmergencyContactPhone,
                    CheckedInAt = DateTime.UtcNow
                };
                await _checkInRepository.AddAsync(checkIn);
            }
            else
            {
                checkIn.CheckedInAt = DateTime.UtcNow;
                await _checkInRepository.UpdateAsync(checkIn);
            }

            await _checkInRepository.SaveChangesAsync();

            return Created($"api/safety/checkin/{checkIn.Id}", ApiResponse<SafetyCheckInResponse>.Created("Check-in", new SafetyCheckInResponse
            {
                CheckInId = checkIn.Id,
                BookingId = checkIn.BookingId,
                IsCheckedIn = true,
                CheckedInAt = checkIn.CheckedInAt
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing check-in");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
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

            // Also alert the saved emergency contact directly (they may not be a TripNest user, so
            // there's no in-app record / preference to attach to).
            var checkIn = await _checkInRepository.GetByBookingIdAsync(request.BookingId);
            if (checkIn != null)
            {
                if (!string.IsNullOrWhiteSpace(checkIn.EmergencyContactPhone))
                    await _smsSender.SendSmsAsync(checkIn.EmergencyContactPhone, alertText);
                if (!string.IsNullOrWhiteSpace(checkIn.EmergencyContactEmail))
                    await _emailSender.SendAsync(checkIn.EmergencyContactEmail, "Emergency alert", $"<p>{alertText}</p>");

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
