using BCrypt.Net;
using TripNest.Core.Context;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public interface IDatabaseSeeder
{
    Task SeedAsync();
}

public class DatabaseSeeder : IDatabaseSeeder
{
    private readonly AppDbContext _context;
    private readonly ILogger<DatabaseSeeder> _logger;
    private int _tripNestSerial;

    public DatabaseSeeder(AppDbContext context, ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            if (_context.Users.Any())
            {
                _logger.LogInformation("Database already seeded");
                return;
            }

            _logger.LogInformation("Starting database seeding...");

            // Create Admin user
            var admin = new User
            {
                Id = Guid.NewGuid().ToString(),
                FullName = "Admin User",
                Email = "admin@tripnest.local",
                Phone = "+233501234567",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123456", 11),
                Role = UserRole.Admin,
                IsVerified = true,
                TripNestId = GenerateTripNestId(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };

            // Create sample Landlords
            var landlord1 = new User
            {
                Id = Guid.NewGuid().ToString(),
                FullName = "Kwame Asante",
                Email = "kwame@tripnest.local",
                Phone = "+233502345678",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Landlord@123456", 11),
                Role = UserRole.Landlord,
                IsVerified = true,
                TripNestId = GenerateTripNestId(),
                Bio = "Experienced property manager in Tarkwa",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                LastLoginAt = DateTime.UtcNow.AddDays(-1)
            };

            var landlord2 = new User
            {
                Id = Guid.NewGuid().ToString(),
                FullName = "Ama Owusu",
                Email = "ama@tripnest.local",
                Phone = "+233503456789",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Landlord@123456", 11),
                Role = UserRole.Landlord,
                IsVerified = false,
                Bio = "New landlord onboarding properties",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                LastLoginAt = DateTime.UtcNow
            };

            // Create sample Tenants
            var tenant1 = new User
            {
                Id = Guid.NewGuid().ToString(),
                FullName = "Kofi Mensah",
                Email = "kofi@tripnest.local",
                Phone = "+233504567890",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Tenant@123456", 11),
                Role = UserRole.Tenant,
                IsVerified = true,
                TripNestId = GenerateTripNestId(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-60),
                LastLoginAt = DateTime.UtcNow.AddDays(-2)
            };

            var tenant2 = new User
            {
                Id = Guid.NewGuid().ToString(),
                FullName = "Yaa Boateng",
                Email = "yaa@tripnest.local",
                Phone = "+233505678901",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Tenant@123456", 11),
                Role = UserRole.Tenant,
                IsVerified = true,
                TripNestId = GenerateTripNestId(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-45),
                LastLoginAt = DateTime.UtcNow
            };

            // Create sample Agent
            var agent = new User
            {
                Id = Guid.NewGuid().ToString(),
                FullName = "Ekow Boadi",
                Email = "ekow@tripnest.local",
                Phone = "+233506789012",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Agent@123456", 11),
                Role = UserRole.Agent,
                IsVerified = true,
                TripNestId = GenerateTripNestId(),
                Bio = "Licensed real estate agent",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-20),
                LastLoginAt = DateTime.UtcNow
            };

            // Create sample Caretaker
            var caretaker = new User
            {
                Id = Guid.NewGuid().ToString(),
                FullName = "Ebo Owusu",
                Email = "ebo@tripnest.local",
                Phone = "+233507890123",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Caretaker@123456", 11),
                Role = UserRole.Caretaker,
                IsVerified = true,
                TripNestId = GenerateTripNestId(),
                Bio = "Professional caretaker - cleaning and maintenance",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-15),
                LastLoginAt = DateTime.UtcNow
            };

            _context.Users.AddRange(admin, landlord1, landlord2, tenant1, tenant2, agent, caretaker);
            await _context.SaveChangesAsync();

            // Create sample Properties
            var property1 = new Property
            {
                Id = Guid.NewGuid().ToString(),
                UserId = landlord1.Id,
                Title = "Cozy Studio in Tarkwa Town Center",
                Description = "Modern, fully furnished studio apartment perfect for short-term stays. Close to market, restaurants, and transport hub.",
                Location = "Tarkwa Town, Ghana",
                Latitude = 5.2802,
                Longitude = -1.5857,
                DailyRate = 150m,
                MonthlyRent = 3500m,
                PropertyType = "Studio",
                Bedrooms = 1,
                Bathrooms = 1,
                Amenities = "WiFi,TV,AirConditioning,Kitchen",
                Status = PropertyStatus.Active,
                CreatedAt = DateTime.UtcNow.AddDays(-20),
                UpdatedAt = DateTime.UtcNow.AddDays(-20)
            };

            var property2 = new Property
            {
                Id = Guid.NewGuid().ToString(),
                UserId = landlord1.Id,
                Title = "2-Bedroom Apartment - Student Friendly",
                Description = "Spacious apartment suitable for students or young professionals. Walking distance to university campus.",
                Location = "University Area, Tarkwa",
                Latitude = 5.2750,
                Longitude = -1.5900,
                DailyRate = 100m,
                MonthlyRent = 2000m,
                PropertyType = "Apartment",
                Bedrooms = 2,
                Bathrooms = 1,
                Amenities = "WiFi,Fan,StudyArea,CommonLiving",
                Status = PropertyStatus.Active,
                CreatedAt = DateTime.UtcNow.AddDays(-15),
                UpdatedAt = DateTime.UtcNow.AddDays(-15)
            };

            var property3 = new Property
            {
                Id = Guid.NewGuid().ToString(),
                UserId = landlord2.Id,
                Title = "3-Bedroom Family Home",
                Description = "Comfortable family home with secure compound. Ideal for long-term stays.",
                Location = "Residential Area, Tarkwa",
                Latitude = 5.2900,
                Longitude = -1.5750,
                MonthlyRent = 5000m,
                DailyRate = null,
                PropertyType = "House",
                Bedrooms = 3,
                Bathrooms = 2,
                Amenities = "Garden,Garage,Security,Maid'sQuarters",
                Status = PropertyStatus.Draft,
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                UpdatedAt = DateTime.UtcNow.AddDays(-3)
            };

            _context.Properties.AddRange(property1, property2, property3);
            await _context.SaveChangesAsync();

            // Create sample Bookings
            var booking1 = new Booking
            {
                Id = Guid.NewGuid().ToString(),
                PropertyId = property1.Id,
                TenantId = tenant1.Id,
                CheckInDate = DateTime.UtcNow.AddDays(5).Date,
                CheckOutDate = DateTime.UtcNow.AddDays(12).Date,
                TotalAmount = 1050m,
                Status = BookingStatus.Completed,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };

            var booking2 = new Booking
            {
                Id = Guid.NewGuid().ToString(),
                PropertyId = property2.Id,
                TenantId = tenant2.Id,
                CheckInDate = DateTime.UtcNow.AddDays(2).Date,
                CheckOutDate = DateTime.UtcNow.AddDays(20).Date,
                TotalAmount = 1800m,
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            };

            _context.Bookings.AddRange(booking1, booking2);
            await _context.SaveChangesAsync();

            // Create sample Escrow Transactions
            var escrow1 = new Escrow
            {
                Id = Guid.NewGuid().ToString(),
                BookingId = booking1.Id,
                Amount = 1050m,
                Status = EscrowStatus.Released,
                ReleasedAt = DateTime.UtcNow.AddDays(-18),
                ReleaseReason = "Post-stay completed",
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };

            var escrow2 = new Escrow
            {
                Id = Guid.NewGuid().ToString(),
                BookingId = booking2.Id,
                Amount = 1800m,
                Status = EscrowStatus.HeldInEscrow,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            };

            _context.Escrows.AddRange(escrow1, escrow2);
            await _context.SaveChangesAsync();

            // Create sample Reviews
            var review1 = new Review
            {
                Id = Guid.NewGuid().ToString(),
                ReviewerId = tenant1.Id,
                RevieweeId = landlord1.Id,
                PropertyId = property1.Id,
                Rating = 5,
                Comment = "Excellent property! Clean, well-maintained, and responsive host.",
                Type = ReviewType.Property,
                CreatedAt = DateTime.UtcNow.AddDays(-15)
            };

            _context.Reviews.Add(review1);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding database");
            throw;
        }
    }

    // Mint sequential IDs through the shared generator so seeded data matches the production
    // format (TN-GH-YYYY-000001…) and never collides with real verification-issued IDs.
    private string GenerateTripNestId() => TripNestIdGenerator.Format(++_tripNestSerial);
}
