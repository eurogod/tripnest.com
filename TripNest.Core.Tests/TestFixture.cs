using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.DTOs.Auth;
using TripNest.Core.Enums;

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
}
