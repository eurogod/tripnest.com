using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Safety;
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
    private readonly ILogger<SafetyController> _logger;

    public SafetyController(
        ISafetyCheckInRepository checkInRepository,
        IBookingRepository bookingRepository,
        ISmsSender smsSender,
        ILogger<SafetyController> logger)
    {
        _checkInRepository = checkInRepository;
        _bookingRepository = bookingRepository;
        _smsSender = smsSender;
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
            var checkIn = await _checkInRepository.GetByBookingIdAsync(request.BookingId);
            if (checkIn?.EmergencyContactPhone == null)
                return BadRequest(ApiResponse<object>.BadRequest("No emergency contact configured"));

            await _smsSender.SendSmsAsync(checkIn.EmergencyContactPhone, "Emergency alert from TripNest tenant. Immediate assistance may be needed.");
            checkIn.AlertSentAt = DateTime.UtcNow;
            await _checkInRepository.UpdateAsync(checkIn);
            await _checkInRepository.SaveChangesAsync();

            return Ok(ApiResponse<object>.Ok("Emergency alert sent", new { }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending emergency alert");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
