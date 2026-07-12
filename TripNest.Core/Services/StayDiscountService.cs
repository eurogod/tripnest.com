using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class StayDiscountService : IStayDiscountService
{
    private readonly ILoyaltyService _loyaltyService;
    private readonly IStudentVerificationService _studentVerificationService;
    private readonly IConfiguration _configuration;

    public StayDiscountService(
        ILoyaltyService loyaltyService,
        IStudentVerificationService studentVerificationService,
        IConfiguration configuration)
    {
        _loyaltyService = loyaltyService;
        _studentVerificationService = studentVerificationService;
        _configuration = configuration;
    }

    public async Task<decimal> GetPercentAsync(string userId, Property property)
    {
        var loyalty = await _loyaltyService.GetDiscountPercentAsync(userId);
        if (property.StayType != StayType.Student)
            return loyalty;

        var student = await _studentVerificationService.IsActiveStudentAsync(userId)
            ? _configuration.GetValue("Student:DiscountPercent", 5m)
            : 0m;
        return Math.Max(loyalty, student);
    }
}
