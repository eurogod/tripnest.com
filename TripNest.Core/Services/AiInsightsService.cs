using Microsoft.Extensions.Caching.Memory;
using TripNest.Core.DTOs.Ai;
using TripNest.Core.DTOs.Search;
using TripNest.Core.Enums;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class AiInsightsService : IAiInsightsService
{
    private static readonly TimeSpan SummaryCacheTtl = TimeSpan.FromHours(24);
    private const int MaxVisionPhotos = 4;
    private const int MaxVideoFrames = 3;
    private const long MaxVisionPhotoBytes = 4_500_000;

    private readonly IAiClient _aiClient;
    private readonly IVideoFrameExtractor _videoFrameExtractor;
    private readonly IPropertyService _propertyService;
    private readonly IPropertyRepository _propertyRepository;
    private readonly IReviewRepository _reviewRepository;
    private readonly IRepository<DamageClaim> _claimRepository;
    private readonly IEscrowRepository _escrowRepository;
    private readonly IRepository<EscrowEvent> _escrowEventRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IAgreementRepository _agreementRepository;
    private readonly IRepository<RoommateProfile> _roommateRepository;
    private readonly IRepository<PropertyPhoto> _photoRepository;
    private readonly IRepository<PricingSettings> _pricingRepository;
    private readonly IUserRepository _userRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AiInsightsService> _logger;

    public AiInsightsService(
        IAiClient aiClient,
        IVideoFrameExtractor videoFrameExtractor,
        IPropertyService propertyService,
        IPropertyRepository propertyRepository,
        IReviewRepository reviewRepository,
        IRepository<DamageClaim> claimRepository,
        IEscrowRepository escrowRepository,
        IRepository<EscrowEvent> escrowEventRepository,
        IBookingRepository bookingRepository,
        IAgreementRepository agreementRepository,
        IRepository<RoommateProfile> roommateRepository,
        IRepository<PropertyPhoto> photoRepository,
        IRepository<PricingSettings> pricingRepository,
        IUserRepository userRepository,
        IFileStorage fileStorage,
        IMemoryCache cache,
        ILogger<AiInsightsService> logger)
    {
        _aiClient = aiClient;
        _videoFrameExtractor = videoFrameExtractor;
        _propertyService = propertyService;
        _propertyRepository = propertyRepository;
        _reviewRepository = reviewRepository;
        _claimRepository = claimRepository;
        _escrowRepository = escrowRepository;
        _escrowEventRepository = escrowEventRepository;
        _bookingRepository = bookingRepository;
        _agreementRepository = agreementRepository;
        _roommateRepository = roommateRepository;
        _photoRepository = photoRepository;
        _pricingRepository = pricingRepository;
        _userRepository = userRepository;
        _fileStorage = fileStorage;
        _cache = cache;
        _logger = logger;
    }

    private void EnsureConfigured()
    {
        if (!_aiClient.IsConfigured)
            throw new ValidationException("AI features are not configured on this server.");
    }

    // ------------------------------------------------------------ 1. review summaries

    private sealed class ReviewSummaryJson { public string? Summary { get; set; } public List<string>? Positives { get; set; } public List<string>? Negatives { get; set; } }

    public async Task<ReviewSummaryResponse> GetReviewSummaryAsync(string propertyId)
    {
        EnsureConfigured();
        _ = await _propertyRepository.GetByIdAsync(propertyId) ?? throw new NotFoundException("Property");

        var reviews = (await _reviewRepository.FindAsync(r => r.PropertyId == propertyId)).ToList();
        if (reviews.Count < 2)
            throw new ValidationException("Not enough reviews to summarise yet.");

        // Keyed on the review count so a new review refreshes the summary; otherwise 24h cache
        // keeps us well inside the provider's free-tier limits.
        var cacheKey = $"ai:review-summary:{propertyId}:{reviews.Count}";
        var cached = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = SummaryCacheTtl;
            var corpus = string.Join("\n---\n", reviews
                .OrderByDescending(r => r.CreatedAt).Take(50)
                .Select(r => $"Rating {r.Rating}/5: {r.Comment}"));
            var raw = await _aiClient.CompleteAsync(
                "You summarise guest reviews for an accommodation listing. Reply ONLY with JSON: " +
                "{\"summary\": string (2 sentences), \"positives\": string[] (max 4 short themes), " +
                "\"negatives\": string[] (max 3 short themes, empty if none)}. Be balanced and specific.",
                corpus);
            return AiJson.TryParse<ReviewSummaryJson>(raw);
        });

        if (cached?.Summary is null)
            throw new ValidationException("Review summary is unavailable right now. Please try again.");

        return new ReviewSummaryResponse
        {
            Summary = cached.Summary,
            Positives = cached.Positives ?? new(),
            Negatives = cached.Negatives ?? new(),
            ReviewCount = reviews.Count
        };
    }

    // ------------------------------------------------------------ 2. admin briefs

    private sealed class BriefJson { public string? Brief { get; set; } public List<string>? KeyPoints { get; set; } public List<string>? Inconsistencies { get; set; } }

    public async Task<AdminBriefResponse> GetClaimBriefAsync(string claimId)
    {
        EnsureConfigured();
        var claim = await _claimRepository.GetByIdAsync(claimId) ?? throw new NotFoundException("Damage claim");
        var booking = await _bookingRepository.GetByIdWithDetailsAsync(claim.BookingId);

        var facts =
            $"DAMAGE CLAIM under review.\nProperty: {booking?.Property?.Title}\n" +
            $"Stay: {booking?.CheckInDate:yyyy-MM-dd} to {booking?.CheckOutDate:yyyy-MM-dd}, total GHS {booking?.TotalAmount:0.00}\n" +
            $"Claimed amount: GHS {claim.Amount:0.00}\n" +
            $"HOST'S CLAIM: {claim.Description}\n" +
            $"TENANT'S RESPONSE: {claim.TenantResponse ?? "(none yet)"}";

        var system =
            "You prepare a NEUTRAL reading brief for a marketplace admin deciding a damage claim. " +
            "Do not decide or recommend an amount. Reply ONLY with JSON: {\"brief\": string (3-4 sentences), " +
            "\"keyPoints\": string[], \"inconsistencies\": string[] (claims vs response vs photos; empty if none)}. " +
            "If photos are attached, describe what is visible and whether it matches the described damage.";

        var photos = await LoadImagesAsync((claim.PhotoPaths ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries));
        var raw = photos.Count > 0
            ? await _aiClient.CompleteWithImagesAsync(system, facts, photos)
            : await _aiClient.CompleteAsync(system, facts);

        var parsed = AiJson.TryParse<BriefJson>(raw)
            ?? throw new ValidationException("The brief is unavailable right now. Please try again.");
        return new AdminBriefResponse
        {
            Brief = parsed.Brief ?? "",
            KeyPoints = parsed.KeyPoints ?? new(),
            Inconsistencies = parsed.Inconsistencies ?? new()
        };
    }

    public async Task<AdminBriefResponse> GetDisputeBriefAsync(string escrowId)
    {
        EnsureConfigured();
        var escrow = await _escrowRepository.GetByIdAsync(escrowId) ?? throw new NotFoundException("Escrow");
        var booking = await _bookingRepository.GetByIdWithDetailsAsync(escrow.BookingId);
        var events = (await _escrowEventRepository.FindAsync(e => e.EscrowId == escrowId))
            .OrderBy(e => e.CreatedAt).ToList();

        var facts =
            $"ESCROW DISPUTE under review.\nProperty: {booking?.Property?.Title}\n" +
            $"Stay: {booking?.CheckInDate:yyyy-MM-dd} to {booking?.CheckOutDate:yyyy-MM-dd}, amount GHS {escrow.Amount:0.00}\n" +
            $"Escrow status: {escrow.Status}. Release/dispute note: {escrow.ReleaseReason ?? "(not recorded)"}\n" +
            "AUDIT TRAIL:\n" +
            string.Join("\n", events.Select(e => $"- {e.CreatedAt:yyyy-MM-dd HH:mm} {e.FromStatus}->{e.ToStatus} by {e.Actor}: {e.Reason}"));

        var raw = await _aiClient.CompleteAsync(
            "You prepare a NEUTRAL reading brief for a marketplace admin arbitrating an escrow dispute. " +
            "Do not decide. Reply ONLY with JSON: {\"brief\": string (3-4 sentences), \"keyPoints\": string[], " +
            "\"inconsistencies\": string[] (empty if none)}.",
            facts);

        var parsed = AiJson.TryParse<BriefJson>(raw)
            ?? throw new ValidationException("The brief is unavailable right now. Please try again.");
        return new AdminBriefResponse
        {
            Brief = parsed.Brief ?? "",
            KeyPoints = parsed.KeyPoints ?? new(),
            Inconsistencies = parsed.Inconsistencies ?? new()
        };
    }

    // ------------------------------------------------------------ 3. natural-language search

    private sealed class CriteriaJson
    {
        public string? Location { get; set; }
        public int? MinBedrooms { get; set; }
        public int? MaxBedrooms { get; set; }
        public string? StayType { get; set; }
        public string? PropertyType { get; set; }
        public string? Amenities { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public DateTime? CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }
    }

    public async Task<NaturalSearchResponse> SearchNaturalAsync(string query)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(query))
            throw new ValidationException("Tell us what you're looking for.");

        var raw = await _aiClient.CompleteAsync(
            "You convert an accommodation search phrase (Ghana context) into filters. Reply ONLY with JSON " +
            "(omit fields the phrase doesn't imply): {\"location\": string, \"minBedrooms\": int, \"maxBedrooms\": int, " +
            "\"stayType\": \"ShortTerm\"|\"LongTerm\"|\"Student\", \"propertyType\": string, " +
            "\"amenities\": string (comma-separated), \"minPrice\": number, \"maxPrice\": number (nightly, GHS), " +
            "\"checkIn\": \"YYYY-MM-DD\", \"checkOut\": \"YYYY-MM-DD\"}. " +
            $"Today is {DateTime.UtcNow:yyyy-MM-dd}; resolve relative dates (\"a weekend in September\" -> the first Fri-Sun of that month ahead).",
            query);

        var parsed = AiJson.TryParse<CriteriaJson>(raw)
            ?? throw new ValidationException("Couldn't understand that search — try rephrasing it.");

        var criteria = new PropertySearchCriteria
        {
            Location = parsed.Location,
            MinBedrooms = parsed.MinBedrooms,
            MaxBedrooms = parsed.MaxBedrooms,
            StayType = Enum.TryParse<StayType>(parsed.StayType, true, out var st) ? st : null,
            PropertyType = parsed.PropertyType,
            Amenities = parsed.Amenities,
            MinPrice = parsed.MinPrice,
            MaxPrice = parsed.MaxPrice,
            // Only sane, future date ranges make it through — a hallucinated range must not filter.
            CheckIn = parsed is { CheckIn: { } ci, CheckOut: { } co } && co > ci && ci >= DateTime.UtcNow.Date ? ci : null,
            CheckOut = parsed is { CheckIn: { } ci2, CheckOut: { } co2 } && co2 > ci2 && ci2 >= DateTime.UtcNow.Date ? co2 : null,
        };

        var results = await _propertyService.SearchPropertiesAsync(criteria, page: 1, pageSize: 20);
        return new NaturalSearchResponse
        {
            Criteria = criteria,
            Results = results.Items.ToList(),
            TotalCount = results.TotalCount
        };
    }

    // ------------------------------------------------------------ 4. agreement summary (reader's language)

    private sealed class AgreementSummaryJson { public string? Summary { get; set; } public List<string>? KeyTerms { get; set; } public List<string>? YourObligations { get; set; } }

    public async Task<AgreementSummaryResponse> GetAgreementSummaryAsync(string agreementId, string userId)
    {
        EnsureConfigured();
        var agreement = await _agreementRepository.GetByIdAsync(agreementId) ?? throw new NotFoundException("Agreement");
        var booking = agreement.Booking ?? await _bookingRepository.GetByIdWithDetailsAsync(agreement.BookingId)
            ?? throw new NotFoundException("Booking associated with agreement");

        var landlordId = booking.Property?.UserId;
        if (booking.TenantId != userId && landlordId != userId)
            throw new ForbiddenException("You are not a party to this agreement");

        var reader = await _userRepository.GetByIdAsync(userId);
        var role = booking.TenantId == userId ? "tenant" : "landlord";
        var language = reader?.PreferredLanguage ?? Language.English;

        var raw = await _aiClient.CompleteAsync(
            $"You explain a rental agreement to the {role} in plain {language}. Do not give legal advice. " +
            "Reply ONLY with JSON (field names in English, values in that language): " +
            "{\"summary\": string (3-4 plain sentences), \"keyTerms\": string[], \"yourObligations\": string[]}.",
            agreement.TermsContent);

        var parsed = AiJson.TryParse<AgreementSummaryJson>(raw)
            ?? throw new ValidationException("The summary is unavailable right now. Please try again.");
        return new AgreementSummaryResponse
        {
            Summary = parsed.Summary ?? "",
            KeyTerms = parsed.KeyTerms ?? new(),
            YourObligations = parsed.YourObligations ?? new()
        };
    }

    // ------------------------------------------------------------ 5+8. listing quality coach (with photo notes)

    private sealed class QualityJson { public List<string>? Suggestions { get; set; } public List<string>? PhotoNotes { get; set; } }

    public async Task<ListingQualityResponse> GetQualityReportAsync(string propertyId, string userId)
    {
        EnsureConfigured();
        var property = await _propertyRepository.GetByIdAsync(propertyId) ?? throw new NotFoundException("Property");
        if (property.UserId != userId)
            throw new ForbiddenException("You do not own this listing");

        var photos = (await _photoRepository.FindAsync(p => p.PropertyId == propertyId)).ToList();
        var pricing = (await _pricingRepository.FindAsync(s => s.PropertyId == propertyId)).FirstOrDefault();

        // The score is deterministic — the model coaches, it doesn't grade.
        var checks = new List<QualityCheck>
        {
            new() { Name = "Photos", Passed = photos.Count >= 4, Detail = $"{photos.Count} photo(s); aim for at least 4." },
            new() { Name = "Description", Passed = property.Description.Length >= 200, Detail = $"{property.Description.Length} characters; aim for 200+." },
            new() { Name = "Amenities", Passed = (property.Amenities ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Length >= 3, Detail = "List at least 3 amenities guests search for." },
            new() { Name = "Pricing rules", Passed = pricing is not null, Detail = pricing is null ? "Set weekend rates, discounts and a cleaning fee on the pricing page." : "Configured." },
            new() { Name = "Verified walkthrough", Passed = property.WalkthroughStatus == WalkthroughStatus.Approved, Detail = $"Walkthrough status: {property.WalkthroughStatus}." },
        };
        var score = (int)Math.Round(100.0 * checks.Count(c => c.Passed) / checks.Count);

        var images = await LoadImagesAsync(photos.Take(MaxVisionPhotos).Select(p => p.PhotoPath));
        var facts =
            $"Listing: {property.Title}\nDescription: {property.Description}\nAmenities: {property.Amenities}\n" +
            $"Type: {property.PropertyType}, {property.Bedrooms} bed / {property.Bathrooms} bath, {property.StayType}.\n" +
            "Coach the host: what would most improve this listing's conversion? If photos are attached, " +
            "note quality issues (blur, darkness, duplicates) and which photo would make the best cover.";
        var system = "You coach accommodation hosts. Reply ONLY with JSON: {\"suggestions\": string[] (max 5, specific), " +
                     "\"photoNotes\": string[] (empty if no photos attached)}.";

        var raw = images.Count > 0
            ? await _aiClient.CompleteWithImagesAsync(system, facts, images)
            : await _aiClient.CompleteAsync(system, facts);
        var parsed = AiJson.TryParse<QualityJson>(raw);

        return new ListingQualityResponse
        {
            Score = score,
            Checks = checks,
            AiSuggestions = parsed?.Suggestions ?? new(),
            PhotoNotes = parsed?.PhotoNotes ?? new()
        };
    }

    // ------------------------------------------------------------ 7. roommate match explanation

    private sealed class RoommateJson { public string? Explanation { get; set; } public List<string>? SharedTraits { get; set; } public List<string>? Considerations { get; set; } }

    public async Task<RoommateExplanationResponse> ExplainRoommateMatchAsync(string userId, string otherUserId)
    {
        EnsureConfigured();
        var mine = (await _roommateRepository.FindAsync(p => p.UserId == userId)).FirstOrDefault()
            ?? throw new ValidationException("Create your roommate profile first");
        var theirs = (await _roommateRepository.FindAsync(p => p.UserId == otherUserId && p.IsVisible)).FirstOrDefault()
            ?? throw new NotFoundException("Roommate profile");

        // Cached per pair — the same two profiles explain the same way until one changes.
        var cacheKey = $"ai:roommate:{userId}:{otherUserId}:{mine.UpdatedAt.Ticks}:{theirs.UpdatedAt.Ticks}";
        var parsed = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = SummaryCacheTtl;
            static string Describe(RoommateProfile p) =>
                $"budget GHS {p.MonthlyBudget}/month, wants {p.PreferredLocation}, " +
                $"{(string.IsNullOrEmpty(p.University) ? "not a student" : $"at {p.University}")}, " +
                $"{(p.NightOwl ? "night owl" : "early riser")}, cleanliness {p.CleanlinessLevel}/5, " +
                $"{(p.Smokes ? "smokes" : "non-smoker")}, {(p.HasPets ? "has pets" : "no pets")}. Bio: {p.Bio}";
            var raw = await _aiClient.CompleteAsync(
                "You explain roommate compatibility warmly and honestly (no scores, no personal data beyond what's given). " +
                "Reply ONLY with JSON: {\"explanation\": string (2-3 sentences), \"sharedTraits\": string[], " +
                "\"considerations\": string[] (things to discuss before moving in together)}.",
                $"PERSON A: {Describe(mine)}\nPERSON B: {Describe(theirs)}");
            return AiJson.TryParse<RoommateJson>(raw);
        });

        if (parsed?.Explanation is null)
            throw new ValidationException("The explanation is unavailable right now. Please try again.");
        return new RoommateExplanationResponse
        {
            Explanation = parsed.Explanation,
            SharedTraits = parsed.SharedTraits ?? new(),
            Considerations = parsed.Considerations ?? new()
        };
    }

    // ------------------------------------------------------------ 9. walkthrough reviewer assist

    private sealed class WalkthroughJson { public bool? Consistent { get; set; } public List<string>? Observations { get; set; } public List<string>? RedFlags { get; set; } }

    public async Task<WalkthroughAiCheckResponse> GetWalkthroughCheckAsync(string propertyId, string reviewerId)
    {
        EnsureConfigured();
        var property = await _propertyRepository.GetByIdAsync(propertyId) ?? throw new NotFoundException("Property");

        // Prefer the walkthrough VIDEO — it's the thing being verified — sampling a few frames when
        // ffmpeg is available; then top up with listing photos. When ffmpeg isn't installed this
        // yields zero frames and the check runs on photos alone, exactly as before.
        var videoFrames = string.IsNullOrEmpty(property.WalkthroughVideoPath)
            ? (IReadOnlyList<AiImage>)Array.Empty<AiImage>()
            : await _videoFrameExtractor.ExtractFramesAsync(property.WalkthroughVideoPath, MaxVideoFrames);

        var photoBudget = Math.Max(0, MaxVisionPhotos - videoFrames.Count);
        var photos = (await _photoRepository.FindAsync(p => p.PropertyId == propertyId)).Take(photoBudget).ToList();
        var photoImages = await LoadImagesAsync(photos.Select(p => p.PhotoPath));

        var images = videoFrames.Concat(photoImages).ToList();
        if (images.Count == 0)
            throw new ValidationException("This listing has no walkthrough video or photos to check against yet.");

        var source = videoFrames.Count > 0
            ? $"{videoFrames.Count} frame(s) sampled from the walkthrough video" +
              (photoImages.Count > 0 ? $" plus {photoImages.Count} listing photo(s)" : "")
            : $"{photoImages.Count} listing photo(s) (no video frames — ffmpeg unavailable or no video)";

        var raw = await _aiClient.CompleteWithImagesAsync(
            "You assist a human reviewer checking an accommodation listing for authenticity (anti-catfishing). " +
            $"The attached images are: {source}. Compare them against the listing facts. Reply ONLY with JSON: " +
            "{\"consistent\": bool, \"observations\": string[] (what the images show), " +
            "\"redFlags\": string[] (stock-photo look, mismatched property type/rooms, watermarks, " +
            "video that doesn't match the photos; empty if none)}.",
            $"Listing facts: {property.Title} — {property.PropertyType}, {property.Bedrooms} bed / {property.Bathrooms} bath " +
            $"in {property.Location}. Description: {property.Description}",
            images);

        var parsed = AiJson.TryParse<WalkthroughJson>(raw)
            ?? throw new ValidationException("The check is unavailable right now. Please try again.");
        return new WalkthroughAiCheckResponse
        {
            PhotosConsistentWithListing = parsed.Consistent ?? false,
            Observations = parsed.Observations ?? new(),
            RedFlags = parsed.RedFlags ?? new(),
            VideoFramesAnalysed = videoFrames.Count
        };
    }

    // ------------------------------------------------------------ 6. maintenance triage

    private sealed class TriageJson { public string? Urgency { get; set; } public string? Category { get; set; } }
    private static readonly string[] Urgencies = { "Low", "Medium", "High", "Emergency" };
    private static readonly string[] Categories = { "Plumbing", "Electrical", "Appliance", "Structural", "Pest", "Security", "Cleaning", "Other" };

    public async Task<(string? Urgency, string? Category)> TriageMaintenanceAsync(string description)
    {
        // Best-effort by contract: triage must never block or fail a maintenance report.
        if (!_aiClient.IsConfigured)
            return (null, null);
        try
        {
            var raw = await _aiClient.CompleteAsync(
                "Classify a rental maintenance report. Reply ONLY with JSON: " +
                $"{{\"urgency\": one of [{string.Join(", ", Urgencies)}], \"category\": one of [{string.Join(", ", Categories)}]}}.",
                description);
            var parsed = AiJson.TryParse<TriageJson>(raw);
            // Whitelist the output — a hallucinated label must not enter the database.
            var urgency = Urgencies.FirstOrDefault(u => string.Equals(u, parsed?.Urgency, StringComparison.OrdinalIgnoreCase));
            var category = Categories.FirstOrDefault(c => string.Equals(c, parsed?.Category, StringComparison.OrdinalIgnoreCase));
            return (urgency, category);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Maintenance triage failed — continuing without it");
            return (null, null);
        }
    }

    // ------------------------------------------------------------ shared

    /// <summary>Best-effort image loading for vision prompts; unreadable/oversized files are skipped.</summary>
    private async Task<List<AiImage>> LoadImagesAsync(IEnumerable<string> paths)
    {
        var images = new List<AiImage>();
        foreach (var path in paths.Take(MaxVisionPhotos))
        {
            try
            {
                await using var stream = await _fileStorage.OpenReadAsync(path.Trim());
                if (stream is null) continue;
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                if (ms.Length is 0 or > MaxVisionPhotoBytes) continue;
                var mediaType = path.Trim().ToLowerInvariant() switch
                {
                    var p when p.EndsWith(".png") => "image/png",
                    var p when p.EndsWith(".webp") => "image/webp",
                    _ => "image/jpeg"
                };
                images.Add(new AiImage(ms.ToArray(), mediaType));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping unreadable image {Path} for AI prompt", path);
            }
        }
        return images;
    }
}
