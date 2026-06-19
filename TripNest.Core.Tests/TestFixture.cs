using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TripNest.Core.Context;
using TripNest.Core.DTOs.Auth;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Tests;

public class TestFixture : WebApplicationFactory<Program>
{
    // Unique per fixture instance so test classes (which run in parallel) each get
    // an isolated in-memory database instead of sharing one named store.
    private readonly string _dbName = $"TripNestTestDb_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the current DbContext registration
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database for testing
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName)
            );

            // Replace the real Twilio/SendGrid senders with recording doubles (singletons so
            // tests can resolve the same instance and inspect what was dispatched).
            services.RemoveAll<ISmsSender>();
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<RecordingSmsSender>();
            services.AddSingleton<RecordingEmailSender>();
            services.AddSingleton<ISmsSender>(sp => sp.GetRequiredService<RecordingSmsSender>());
            services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<RecordingEmailSender>());
        });

        builder.UseEnvironment("Testing");
    }
}

public class TestBase : IAsyncLifetime
{
    protected readonly TestFixture _fixture;
    protected HttpClient _httpClient;

    public TestBase()
    {
        _fixture = new TestFixture();
        _httpClient = _fixture.CreateClient();
    }

    public virtual async Task InitializeAsync()
    {
        using var scope = _fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public virtual async Task DisposeAsync()
    {
        using var scope = _fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        _fixture.Dispose();
    }

    /// <summary>
    /// Registers a fresh user with the given role, logs in, sets the bearer token on
    /// the shared HttpClient, and returns (userId, accessToken). Each call uses a unique
    /// email so tests don't collide.
    /// </summary>
    protected async Task<(string UserId, string Token)> RegisterAndLoginAsync(
        UserRole role, string? email = null)
    {
        email ??= $"user_{Guid.NewGuid():N}@example.com";

        var register = new RegisterRequest
        {
            FullName = $"{role} User",
            Email = email,
            Password = "Password@123",
            ConfirmPassword = "Password@123",
            Phone = "+233501234567",
            Role = role
        };
        await _httpClient.PostAsJsonAsync("/api/auth/register", register);

        var login = new LoginRequest { Email = email, Password = "Password@123" };
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", login);
        var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data");

        var token = data.GetProperty("accessToken").GetString()!;
        var userId = data.GetProperty("userId").GetString()!;
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return (userId, token);
    }

    protected void ClearAuth() => _httpClient.DefaultRequestHeaders.Authorization = null;

    /// <summary>
    /// Flags a user as identity-verified directly in the database — used to exercise the
    /// gated Landlord/Agent/Caretaker actions that <c>[RequireVerified]</c> protects, since
    /// real verification runs asynchronously against external sidecars.
    /// </summary>
    protected async Task MarkUserVerifiedAsync(string userId)
    {
        using var scope = _fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.Users.FindAsync(userId);
        if (user != null)
        {
            user.IsVerified = true;
            await dbContext.SaveChangesAsync();
        }
    }
}
