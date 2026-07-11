using TripNest.Core.DTOs.Roommates;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class RoommateService : IRoommateService
{
    private readonly IRepository<RoommateProfile> _profileRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<RoommateService> _logger;

    public RoommateService(
        IRepository<RoommateProfile> profileRepository,
        IUserRepository userRepository,
        ILogger<RoommateService> logger)
    {
        _profileRepository = profileRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<RoommateProfileResponse> UpsertMyProfileAsync(string userId, UpsertRoommateProfileRequest request)
    {
        var profile = (await _profileRepository.FindAsync(p => p.UserId == userId)).FirstOrDefault();
        var isNew = profile is null;
        profile ??= new RoommateProfile { UserId = userId, PreferredLocation = request.PreferredLocation };

        profile.Bio = request.Bio;
        profile.University = request.University;
        profile.PreferredLocation = request.PreferredLocation;
        profile.MonthlyBudget = request.MonthlyBudget;
        profile.MoveInDate = request.MoveInDate;
        profile.Smokes = request.Smokes;
        profile.OkWithSmoker = request.OkWithSmoker;
        profile.HasPets = request.HasPets;
        profile.OkWithPets = request.OkWithPets;
        profile.NightOwl = request.NightOwl;
        profile.CleanlinessLevel = request.CleanlinessLevel;
        profile.IsVisible = request.IsVisible;
        profile.UpdatedAt = DateTime.UtcNow;

        if (isNew)
            await _profileRepository.AddAsync(profile);
        else
            await _profileRepository.UpdateAsync(profile);
        await _profileRepository.SaveChangesAsync();

        _logger.LogInformation("Roommate profile {Action} for user {UserId}", isNew ? "created" : "updated", userId);
        return await MapAsync(profile);
    }

    public async Task<RoommateProfileResponse> GetMyProfileAsync(string userId)
    {
        var profile = (await _profileRepository.FindAsync(p => p.UserId == userId)).FirstOrDefault()
            ?? throw new NotFoundException("Roommate profile");
        return await MapAsync(profile);
    }

    public async Task DeleteMyProfileAsync(string userId)
    {
        var profile = (await _profileRepository.FindAsync(p => p.UserId == userId)).FirstOrDefault()
            ?? throw new NotFoundException("Roommate profile");
        await _profileRepository.DeleteAsync(profile);
        await _profileRepository.SaveChangesAsync();
    }

    public async Task<PagedResult<RoommateMatchResponse>> GetMatchesAsync(
        string userId, string? location, decimal? maxBudget, string? university, int page, int pageSize)
    {
        // Reciprocity: you must be discoverable to discover others.
        var mine = (await _profileRepository.FindAsync(p => p.UserId == userId)).FirstOrDefault();
        if (mine is null)
            throw new ValidationException("Create your roommate profile first — matching is mutual");
        if (!mine.IsVisible)
            throw new ValidationException("Your profile is hidden — make it visible to browse matches");

        // DB-filter what translates cleanly; habit conflicts and scoring are in-memory over the
        // narrowed set (roommate seekers per city is a small population).
        var locationFilter = (location ?? "").Trim().ToLower();
        var universityFilter = (university ?? "").Trim().ToLower();
        var candidates = await _profileRepository.FindAsync(p =>
            p.UserId != userId &&
            p.IsVisible &&
            (locationFilter == "" || p.PreferredLocation.ToLower().Contains(locationFilter)) &&
            (universityFilter == "" || (p.University != null && p.University.ToLower().Contains(universityFilter))) &&
            (maxBudget == null || p.MonthlyBudget <= maxBudget));

        var scored = candidates
            .Where(c => !HasHardConflict(mine, c))
            .Select(c => (Profile: c, Score: Score(mine, c)))
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Profile.UpdatedAt)
            .ToList();

        var paged = Paging.Page(scored, page, pageSize);

        // Enrich the page with names + identity-verification badges in one query.
        var userIds = paged.Items.Select(x => x.Profile.UserId).ToList();
        var users = (await _userRepository.FindAsync(u => userIds.Contains(u.Id))).ToDictionary(u => u.Id);

        return new PagedResult<RoommateMatchResponse>
        {
            Items = paged.Items.Select(x => new RoommateMatchResponse
            {
                Profile = Map(x.Profile, users.GetValueOrDefault(x.Profile.UserId)),
                Score = x.Score
            }).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    /// <summary>A pairing that can't work regardless of score — filtered out, in both directions.</summary>
    private static bool HasHardConflict(RoommateProfile a, RoommateProfile b) =>
        (a.Smokes && !b.OkWithSmoker) || (b.Smokes && !a.OkWithSmoker) ||
        (a.HasPets && !b.OkWithPets) || (b.HasPets && !a.OkWithPets);

    /// <summary>
    /// 0–100 compatibility: budget proximity (35), location overlap (25), same university (15),
    /// sleep-schedule match (15), cleanliness proximity (10). Deliberately simple and explainable —
    /// a roommate decision is made in chat, the score just orders the list sensibly.
    /// </summary>
    private static int Score(RoommateProfile mine, RoommateProfile other)
    {
        var score = 0d;

        var maxBudget = Math.Max(mine.MonthlyBudget, other.MonthlyBudget);
        if (maxBudget > 0)
            score += 35 * (1 - (double)(Math.Abs(mine.MonthlyBudget - other.MonthlyBudget) / maxBudget));

        var mineLoc = mine.PreferredLocation.Trim().ToLowerInvariant();
        var otherLoc = other.PreferredLocation.Trim().ToLowerInvariant();
        if (mineLoc.Length > 0 && (mineLoc.Contains(otherLoc) || otherLoc.Contains(mineLoc)))
            score += 25;

        if (!string.IsNullOrWhiteSpace(mine.University) &&
            string.Equals(mine.University.Trim(), other.University?.Trim(), StringComparison.OrdinalIgnoreCase))
            score += 15;

        if (mine.NightOwl == other.NightOwl)
            score += 15;

        score += 10 * (1 - Math.Abs(mine.CleanlinessLevel - other.CleanlinessLevel) / 4d);

        return (int)Math.Round(score);
    }

    private async Task<RoommateProfileResponse> MapAsync(RoommateProfile profile) =>
        Map(profile, await _userRepository.GetByIdAsync(profile.UserId));

    private static RoommateProfileResponse Map(RoommateProfile p, User? user) => new()
    {
        UserId = p.UserId,
        FullName = user?.FullName,
        IsVerified = user?.IsVerified ?? false,
        Bio = p.Bio,
        University = p.University,
        PreferredLocation = p.PreferredLocation,
        MonthlyBudget = p.MonthlyBudget,
        MoveInDate = p.MoveInDate,
        Smokes = p.Smokes,
        OkWithSmoker = p.OkWithSmoker,
        HasPets = p.HasPets,
        OkWithPets = p.OkWithPets,
        NightOwl = p.NightOwl,
        CleanlinessLevel = p.CleanlinessLevel,
        IsVisible = p.IsVisible
    };
}
