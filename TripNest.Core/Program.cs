using System.Text;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.FileProviders;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RedisRateLimiting;
using Serilog;
using StackExchange.Redis;
using TripNest.Core.Caching;
using TripNest.Core.Context;
using TripNest.Core.Storage;
using TripNest.Core.Extensions;
using TripNest.Core.Middleware;
using TripNest.Core.Monitoring;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;
using TripNest.Core.Repositories;
using TripNest.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Structured logging via Serilog. Console is always on (human-readable locally; the structured
// properties — including the trace ids added by ActivityEnricher — make it greppable/queryable).
// Clear the default Console/Debug providers first so logs aren't emitted twice; writeToProviders:true
// still forwards events to other registered providers — notably the OpenTelemetry/Azure Monitor
// provider wired below — so the same logs also reach Application Insights.
builder.Logging.ClearProviders();
builder.Host.UseSerilog((context, _, config) => config
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.With<ActivityEnricher>()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} <trace:{TraceId}>{NewLine}{Exception}"),
    writeToProviders: true);

// QuestPDF Community licence (free for organisations/individuals under the revenue threshold).
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

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
var jwtKey = jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key is not configured");

// Refuse to boot outside Development with a weak or well-known signing key. The value below is the
// placeholder that ships in appsettings.json for local dev — if it (or anything < 32 bytes) reaches
// a real environment, anyone could forge tokens, so fail fast and force a proper secret via env/secrets.
const string InsecureDefaultJwtKey = "your-super-secret-key-must-be-at-least-32-characters-long-12345";
if (!builder.Environment.IsDevelopment() && (jwtKey == InsecureDefaultJwtKey || jwtKey.Length < 32))
    throw new InvalidOperationException(
        "Jwt:Key must be set to a strong, unique secret (at least 32 characters) outside Development. " +
        "Configure it via environment variable Jwt__Key or user-secrets — never the committed default.");

// A missing Paystack secret swaps in SimulatedPaymentGateway, which pretends every charge and
// verification succeeded — convenient for local dev and tests, but catastrophic anywhere real
// bookings exist: it would report money as received that was never paid. Only Development may
// run without the key; Staging/Production/anything else must fail fast.
var paystackConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["PaystackSettings:SecretKey"]);
if (!builder.Environment.IsDevelopment() && !paystackConfigured)
    throw new InvalidOperationException(
        "PaystackSettings:SecretKey must be configured outside Development — the simulated gateway " +
        "pretends payments succeeded and must never run against real bookings. Set it via env/secrets.");

var key = Encoding.UTF8.GetBytes(jwtKey);

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
        // Access tokens are bearer credentials valid until they expire, so a deactivated account would
        // otherwise keep access until then. Re-check IsActive against the database on each validated
        // token and reject deactivated users immediately. (One indexed primary-key lookup per request.)
        OnTokenValidated = async context =>
        {
            var userId = context.Principal.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                context.Fail("Token is missing a subject claim.");
                return;
            }

            var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
            var account = await userRepository.GetByIdAsync(userId);
            if (account is null || !account.IsActive)
                context.Fail("The account is no longer active.");
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
// Same pattern for non-emergency notification delivery (SMS/email sent off the request thread).
builder.Services.AddSingleton<INotificationDispatchQueue, NotificationDispatchQueue>();
builder.Services.AddScoped<IPropertyService, PropertyService>();
builder.Services.AddScoped<IWalkthroughService, WalkthroughService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<ITrustScoreService, TrustScoreService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();
builder.Services.AddScoped<ICancellationPolicyService, CancellationPolicyService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<IPhoneNumberValidator, PhoneNumberValidator>();
builder.Services.AddScoped<IPhoneVerificationService, PhoneVerificationService>();
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
builder.Services.AddHttpClient<ISmsSender, TextBeeSmsSender>();
builder.Services.AddHttpClient<INiaClient, NiaClient>();
// Real gateway when a key exists; otherwise the dev-only simulator (guarded above: unconfigured
// is impossible outside Development). Keeps the test-mode branch out of the real money path.
if (paystackConfigured)
    builder.Services.AddHttpClient<IPaymentGateway, PaystackPaymentGateway>();
else
    builder.Services.AddSingleton<IPaymentGateway, SimulatedPaymentGateway>();
builder.Services.AddHttpClient<IFaceMatchClient, FaceMatchClient>();
builder.Services.AddHttpClient<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddHttpClient<IFacebookAuthService, FacebookAuthService>();

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

// Distributed tracing + metrics via OpenTelemetry, exported to Azure Application Insights when a
// connection string is configured (env APPLICATIONINSIGHTS_CONNECTION_STRING or
// ApplicationInsights:ConnectionString). Without it, instrumentation still runs but exports nowhere,
// so local/dev and tests need no Azure account.
var appInsightsConn = builder.Configuration["ApplicationInsights:ConnectionString"]
    ?? builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

var otel = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("TripNest.Core"));

// Signals the Azure Monitor distro does NOT add: DB spans (Npgsql's built-in source) + runtime metrics.
otel.WithTracing(tracing => tracing.AddSource("Npgsql"))
    .WithMetrics(metrics => metrics.AddRuntimeInstrumentation());

if (!string.IsNullOrWhiteSpace(appInsightsConn))
{
    // The distro adds ASP.NET Core + HttpClient instrumentation and exports traces, metrics and logs.
    otel.UseAzureMonitor(options => options.ConnectionString = appInsightsConn);
}
else
{
    // No Azure backend configured — still instrument so traces/metrics exist for any other exporter
    // (and so the wiring is identical between environments). Added here to avoid double-registering
    // the same instrumentation the distro provides above.
    otel.WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation())
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation());
}

// Typed, validated configuration. ValidateOnStart makes a bad value (fee out of range, blank
// currency, absurd grace period) fail the boot instead of surfacing mid-payout, and gives every
// consumer one source of truth instead of scattered raw IConfiguration reads with per-site defaults.
builder.Services.AddOptions<TripNest.Core.Options.PlatformOptions>()
    .BindConfiguration(TripNest.Core.Options.PlatformOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<TripNest.Core.Options.EscrowOptions>()
    .BindConfiguration(TripNest.Core.Options.EscrowOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

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
builder.Services.AddScoped<IDashboardStatsService, DashboardStatsService>();
builder.Services.AddScoped<IAssistantService, AssistantService>();
builder.Services.AddScoped<IScamDetectionService, ScamDetectionService>();
// AI provider. Forced to Gemini (Google AI Studio free tier — Ai:Gemini:ApiKey, no card needed).
// Claude is commented out for now; restore the if/else to re-enable provider selection via Ai:Provider.
builder.Services.AddHttpClient<IAiClient, GeminiAiClient>();
// if (string.Equals(builder.Configuration["Ai:Provider"], "gemini", StringComparison.OrdinalIgnoreCase))
//     builder.Services.AddHttpClient<IAiClient, GeminiAiClient>();
// else
//     builder.Services.AddSingleton<IAiClient, ClaudeAiClient>();

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 104_857_600; // 100 MB — walkthrough videos; cap guards against memory/disk exhaustion
});

builder.Services.AddScoped<IRepository<ServiceRequest>, Repository<ServiceRequest>>();
builder.Services.AddScoped<IRepository<ViewingRequest>, Repository<ViewingRequest>>();
builder.Services.AddScoped<IRepository<PropertyBlockedDate>, Repository<PropertyBlockedDate>>();
builder.Services.AddScoped<IRepository<WishlistItem>, Repository<WishlistItem>>();

// Marketplace / operations modules (frontend parity).
builder.Services.AddScoped<IRepository<PricingSettings>, Repository<PricingSettings>>();
builder.Services.AddScoped<IRepository<Inquiry>, Repository<Inquiry>>();
builder.Services.AddScoped<IRepository<SavedPaymentMethod>, Repository<SavedPaymentMethod>>();
builder.Services.AddScoped<IRepository<ExchangePost>, Repository<ExchangePost>>();
builder.Services.AddScoped<IRepository<ExchangeReply>, Repository<ExchangeReply>>();
builder.Services.AddScoped<IRepository<HostTask>, Repository<HostTask>>();
builder.Services.AddScoped<IRepository<TeamMember>, Repository<TeamMember>>();
builder.Services.AddScoped<IRepository<ResourceItem>, Repository<ResourceItem>>();
builder.Services.AddScoped<IRepository<PropertyTour>, Repository<PropertyTour>>();

// Host disbursements (Paystack Transfers).
builder.Services.AddScoped<IRepository<Payout>, Repository<Payout>>();
builder.Services.AddScoped<IRepository<PayoutAccount>, Repository<PayoutAccount>>();
builder.Services.AddScoped<IPayoutService, PayoutService>();

builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<ICalendarService, CalendarService>();
builder.Services.AddScoped<IInquiryService, InquiryService>();
builder.Services.AddScoped<IPaymentMethodService, PaymentMethodService>();
builder.Services.AddScoped<IExchangeService, ExchangeService>();
builder.Services.AddScoped<IHostTaskService, HostTaskService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IResourceService, ResourceService>();
builder.Services.AddScoped<IStatementService, StatementService>();
builder.Services.AddScoped<ITourService, TourService>();
builder.Services.AddScoped<ILandlordWorkspaceService, LandlordWorkspaceService>();

// Register background services
builder.Services.AddHostedService<EscrowAutoReleaseService>();
builder.Services.AddHostedService<TrustScoreDailySnapshotService>();
builder.Services.AddHostedService<VerificationProcessingService>();
builder.Services.AddHostedService<NotificationDispatchService>();

// Shared Redis connection (optional). When Redis:ConnectionString is set, the SignalR backplane,
// output cache and rate limiter all use it, so the app is correct across multiple instances. When
// it isn't set, each falls back to in-memory single-instance behaviour. AbortOnConnectFail=false lets
// the app start even if Redis is momentarily unreachable.
var redisConnection = builder.Configuration["Redis:ConnectionString"];
IConnectionMultiplexer? redisMux = null;
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    var redisOptions = ConfigurationOptions.Parse(redisConnection);
    redisOptions.AbortOnConnectFail = false;
    redisMux = ConnectionMultiplexer.Connect(redisOptions);
    builder.Services.AddSingleton<IConnectionMultiplexer>(redisMux);
}

// Tracks online/offline for chat presence (in-memory, per-instance).
builder.Services.AddSingleton<TripNest.Core.Hubs.IPresenceTracker, TripNest.Core.Hubs.PresenceTracker>();

// SignalR: a Redis backplane is required to scale the chat hub beyond one instance (Groups/Clients
// are per-server otherwise). Reuse the shared multiplexer when present.
var signalR = builder.Services.AddSignalR();
if (redisMux is not null)
    signalR.AddStackExchangeRedis(o => o.ConnectionFactory = _ => Task.FromResult<IConnectionMultiplexer>(redisMux));

// Liveness/readiness checks for orchestrators and load balancers. Readiness probes the real
// dependencies: Postgres is critical (Unhealthy → 503, instance pulled from rotation); the
// verification sidecars are Degraded-but-non-gating (only needed for Ghana Card verification, so the
// API stays "ready" when they're down but the degradation is still reported for monitoring).
var tripNestIdUrl = builder.Configuration["Services:TripNestId"] ?? "http://localhost:5135";
var faceMatchUrl = builder.Configuration["Services:FaceMatchSidecar"] ?? "http://localhost:5001";

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("postgres", failureStatus: HealthStatus.Unhealthy, tags: new[] { "ready" })
    .AddTypeActivatedCheck<HttpDependencyHealthCheck>("tripnest-id", HealthStatus.Degraded, new[] { "ready" }, args: tripNestIdUrl)
    .AddTypeActivatedCheck<HttpDependencyHealthCheck>("face-match", HealthStatus.Degraded, new[] { "ready" }, args: faceMatchUrl);

// In-process caching. MemoryCache is available for service-level caching; OutputCache caches whole
// HTTP responses for the public, non-personalized GET endpoints (opted in per-endpoint via
// [OutputCache(PolicyName=...)]). Only anonymous, identical-for-everyone reads are annotated, so a
// shared cached response is always correct. TTLs are short; tag-based eviction on writes can be
// layered on later for instant freshness.
builder.Services.AddMemoryCache();
builder.Services.AddOutputCache(options =>
{
    // Rarely-changing app configuration.
    options.AddPolicy("config", b => b.Expire(TimeSpan.FromMinutes(5)));
    // Public listings/details/search — vary by query so each filter combination is cached distinctly.
    options.AddPolicy("listings", b => b
        .Expire(TimeSpan.FromSeconds(60))
        .SetVaryByQuery("*"));
});

// Share the output cache across instances when Redis is available (registered after AddOutputCache so
// it replaces the default in-memory store); otherwise the in-memory store is used.
if (redisMux is not null)
    builder.Services.AddSingleton<IOutputCacheStore>(_ => new RedisOutputCacheStore(redisMux));

// File storage: Azure Blob when configured (multi-instance-safe; survives restarts/scale-out), else
// local disk under wwwroot served by UseStaticFiles (single-instance/dev).
var blobConnection = builder.Configuration["Storage:Blob:ConnectionString"];
if (!string.IsNullOrWhiteSpace(blobConnection))
    builder.Services.AddSingleton<IFileStorage>(_ =>
        new BlobFileStorage(blobConnection, builder.Configuration["Storage:Blob:Container"] ?? "uploads"));
else
    builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

// Edge rate limiting: a coarse fixed-window abuse guard. Backed by Redis when configured (shared
// counters across instances → a true global limit); in-memory otherwise (per-instance ceiling).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    static string PartitionKey(HttpContext ctx) =>
        ctx.User.GetUserId() ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

    // Build a fixed-window partition for the caller — Redis-backed when available, else in-memory.
    System.Threading.RateLimiting.RateLimitPartition<string> Partition(HttpContext ctx, int permitLimit) =>
        redisMux is not null
            ? RedisRateLimitPartition.GetFixedWindowRateLimiter(PartitionKey(ctx), _ => new RedisFixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                ConnectionMultiplexerFactory = () => redisMux
            })
            : System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(PartitionKey(ctx),
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromMinutes(1)
                });

    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(
        ctx => Partition(ctx, 100));

    // Tighter, per-user limit for OTP sends (defense-in-depth on top of the service cooldown),
    // so a caller can't fan out SMS even by rotating fast. Opt-in via [EnableRateLimiting("otp")].
    options.AddPolicy("otp", ctx => Partition(ctx, 5));

    // AI endpoints call an external model per request — cap per-user so one caller can't burn
    // the provider quota (or budget). Opt-in via [EnableRateLimiting("ai")].
    options.AddPolicy("ai", ctx => Partition(ctx, 10));
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

// Honour X-Forwarded-* from the hosting reverse proxy (Azure App Service, etc.) so the real client
// IP and scheme are used — needed for correct rate-limiting partitions, HTTPS detection and logs.
// Must run before any middleware that inspects the connection.
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};

// Only honour X-Forwarded-* from proxies we explicitly trust. Blindly trusting the header (clearing
// the known-proxy list) lets any client spoof their IP — which would defeat the per-IP rate limiter
// and forge the client IP in logs. Configure the real reverse-proxy addresses/ranges via
// ForwardedHeaders:KnownProxies / ForwardedHeaders:KnownNetworks (e.g. the Azure App Service subnet);
// when none are configured we fall back to the framework default (loopback only), which is safe.
var knownProxies = app.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>();
var knownNetworks = app.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>();
if ((knownProxies is { Length: > 0 }) || (knownNetworks is { Length: > 0 }))
{
    forwardedOptions.KnownProxies.Clear();
    forwardedOptions.KnownNetworks.Clear();

    foreach (var proxy in knownProxies ?? Array.Empty<string>())
        if (System.Net.IPAddress.TryParse(proxy, out var ip))
            forwardedOptions.KnownProxies.Add(ip);

    // Each network is "baseAddress/prefixLength", e.g. "10.0.0.0/8".
    foreach (var network in knownNetworks ?? Array.Empty<string>())
    {
        var parts = network.Split('/', 2);
        if (parts.Length == 2 && System.Net.IPAddress.TryParse(parts[0], out var baseIp) && int.TryParse(parts[1], out var prefix))
            forwardedOptions.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(baseIp, prefix));
    }
}
app.UseForwardedHeaders(forwardedOptions);

// Baseline security response headers on every response (set via OnStarting so they survive the
// exception handler's response rewrite).
app.UseMiddleware<SecurityHeadersMiddleware>();

// Catch-all error translation must wrap the whole pipeline.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// One structured summary line per request (method, path, status, elapsed) with the trace id —
// the backbone of request-level monitoring.
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TripNest.Core API v1");
        c.RoutePrefix = string.Empty;
    });
}
else
{
    // Outside Development, enforce HTTPS at the browser via HSTS.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Serve locally-stored uploads (/uploads/...). Ensure the web root exists so the static file
// provider has a directory to serve even before the first upload.
var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(webRoot);
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(webRoot) });

app.UseCors("AllowFrontend");

// Output caching must run after CORS. Only the explicitly-annotated public GET endpoints are cached;
// everything else passes straight through to auth below.
app.UseOutputCache();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

// Liveness: the process is up and serving (no dependency checks — used for restart decisions).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
// Readiness: dependencies are usable (DB gates; sidecars reported but non-gating).
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
// Back-compat: the original path returns the full report.
app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<TripNest.Core.Hubs.ChatHub>("/hubs/chat");

app.Run();

// Exposed so the integration test project can reference it via WebApplicationFactory<Program>.
// Top-level statements otherwise compile Program as an internal class.
public partial class Program { }
