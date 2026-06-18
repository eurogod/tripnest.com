using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;

namespace TripNest.Core.Tests;

public class TestFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Builder.IWebHostBuilder builder)
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
                options.UseInMemoryDatabase("TripNestTestDb")
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

    public async Task InitializeAsync()
    {
        using var scope = _fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        using var scope = _fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        _fixture.Dispose();
    }
}
