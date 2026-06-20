using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TripNest.Core.Context;
using TripNest.Core.Extensions;
using TripNest.Core.Middleware;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;
using TripNest.Core.Repositories;
using TripNest.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        // Cloud Postgres (e.g. Azure Flexible Server) prunes idle connections, so a pooled
        // connection can be dead by the time it's reused. Retry transient failures instead of 500ing.
        npgsql => npgsql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null)
    )
);

var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key is not configured"));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        NameClaimType = System.Security.Claims.ClaimTypes.Name,
        RoleClaimType = System.Security.Claims.ClaimTypes.Role
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("JWT authentication failed: {Message}", context.Exception.Message);
            return Task.CompletedTask;
        },
        // SignalR over WebSockets can't send an Authorization header (browsers forbid it on
        // the WS handshake), so the JS client passes the token as ?access_token=... instead.
        // Read it from the query string for hub requests so the [Authorize] ChatHub works.
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:3000" })
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IVerificationRepository, VerificationRepository>();
builder.Services.AddScoped<IPropertyRepository, PropertyRepository>();
builder.Services.AddScoped<IWalkthroughRepository, WalkthroughRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IEscrowRepository, EscrowRepository>();
builder.Services.AddScoped<IAgreementRepository, AgreementRepository>();
builder.Services.AddScoped<IMaintenanceRepository, MaintenanceRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<ICaretakerRepository, CaretakerRepository>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IAgentRepository, AgentRepository>();
builder.Services.AddScoped<IReceiptRepository, ReceiptRepository>();
builder.Services.AddScoped<ISafetyCheckInRepository, SafetyCheckInRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<ITrustScoreSnapshotRepository, TrustScoreSnapshotRepository>();
builder.Services.AddScoped<IStayFeedbackRepository, StayFeedbackRepository>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IVerificationService, VerificationService>();
// Singleton so the request path (enqueue) and the hosted processor (dequeue) share one queue.
builder.Services.AddSingleton<IVerificationQueue, VerificationQueue>();
builder.Services.AddScoped<IPropertyService, PropertyService>();
builder.Services.AddScoped<IWalkthroughService, WalkthroughService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<ITrustScoreService, TrustScoreService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ISmsSender, TwilioSmsSender>();
builder.Services.AddScoped<IEmailSender, SendGridEmailSender>();
builder.Services.AddHttpClient<INiaClient, NiaClient>();
builder.Services.AddHttpClient<IPaymentGateway, PaystackPaymentGateway>();
builder.Services.AddHttpClient<IFaceMatchClient, FaceMatchClient>();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TripNest.Core API",
        Version = "v1",
        Description = "Backend API for TripNest - Accommodation Booking Platform"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter JWT token"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
});

// Register DatabaseSeeder
builder.Services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();

// Module service implementations
builder.Services.AddScoped<IEscrowService, EscrowService>();
builder.Services.AddScoped<IAgreementService, AgreementService>();
builder.Services.AddScoped<ICaretakerService, CaretakerService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IReceiptService, ReceiptService>();
builder.Services.AddScoped<IChatService, ChatService>();

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 524_288_000; // 500 MB
});

builder.Services.AddScoped<IRepository<ServiceRequest>, Repository<ServiceRequest>>();
builder.Services.AddScoped<IRepository<ViewingRequest>, Repository<ViewingRequest>>();
builder.Services.AddScoped<IRepository<PropertyBlockedDate>, Repository<PropertyBlockedDate>>();
builder.Services.AddScoped<IRepository<WishlistItem>, Repository<WishlistItem>>();

// Register background services
builder.Services.AddHostedService<EscrowAutoReleaseService>();
builder.Services.AddHostedService<TrustScoreDailySnapshotService>();
builder.Services.AddHostedService<VerificationProcessingService>();

// Register SignalR. A Redis backplane is required to scale the chat hub beyond a single
// instance (Groups/Clients are per-server otherwise); enable it by setting Redis:ConnectionString.
var signalR = builder.Services.AddSignalR();
var redisConnection = builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    signalR.AddStackExchangeRedis(redisConnection);
}

// Liveness/readiness endpoint for orchestrators and load balancers.
builder.Services.AddHealthChecks();

// Edge rate limiting: a global fixed-window limiter as a coarse abuse guard.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.GetUserId()
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Auto-migrate is convenient in development. In production, apply migrations out-of-band
    // (CI/CD) and set Database:AutoMigrate=false so multiple instances don't race on startup.
    var autoMigrate = app.Configuration.GetValue("Database:AutoMigrate", app.Environment.IsDevelopment());
    if (autoMigrate)
        dbContext.Database.Migrate();

    // Demo data includes well-known credentials — it must NEVER be seeded outside Development.
    if (app.Environment.IsDevelopment())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
        await seeder.SeedAsync();
    }
}

// Catch-all error translation must wrap the whole pipeline.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TripNest.Core API v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<TripNest.Core.Hubs.ChatHub>("/hubs/chat");

app.Run();

// Exposed so the integration test project can reference it via WebApplicationFactory<Program>.
// Top-level statements otherwise compile Program as an internal class.
public partial class Program { }
