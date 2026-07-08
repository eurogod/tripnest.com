using TripNest.Core.DTOs.Agents;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Enums;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class AgentService : IAgentService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    // Who may move a viewing request from which status to which. Anything not listed is rejected,
    // so a tenant can no longer mark their own request Confirmed/Completed.
    private static readonly Dictionary<ViewingRequestStatus, ViewingRequestStatus[]> AgentTransitions = new()
    {
        [ViewingRequestStatus.Pending] = new[] { ViewingRequestStatus.Confirmed, ViewingRequestStatus.Declined },
        [ViewingRequestStatus.Confirmed] = new[] { ViewingRequestStatus.Completed },
    };

    private static readonly Dictionary<ViewingRequestStatus, ViewingRequestStatus[]> TenantTransitions = new()
    {
        [ViewingRequestStatus.Pending] = new[] { ViewingRequestStatus.Cancelled },
        [ViewingRequestStatus.Confirmed] = new[] { ViewingRequestStatus.Cancelled },
    };

    private readonly IAgentRepository _agentRepository;
    private readonly IPropertyRepository _propertyRepository;
    private readonly IRepository<ViewingRequest> _viewingRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AgentService> _logger;

    public AgentService(
        IAgentRepository agentRepository,
        IPropertyRepository propertyRepository,
        IRepository<ViewingRequest> viewingRequestRepository,
        IUserRepository userRepository,
        INotificationService notificationService,
        ILogger<AgentService> logger)
    {
        _agentRepository = agentRepository;
        _propertyRepository = propertyRepository;
        _viewingRequestRepository = viewingRequestRepository;
        _userRepository = userRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<PagedResult<AgentResponse>> GetVerifiedAgentsAsync(string? serviceArea, int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize is 0 ? DefaultPageSize : pageSize, 1, MaxPageSize);

        var active = (await _agentRepository.FindAsync(a => a.Status == AgentStatus.Active)).AsEnumerable();

        if (!string.IsNullOrWhiteSpace(serviceArea))
            active = active.Where(a => a.ServiceArea != null && a.ServiceArea.Contains(serviceArea, StringComparison.OrdinalIgnoreCase));

        var filtered = active.OrderByDescending(a => a.JoinDate).ToList();
        var pageItems = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var ratings = await GetRatingAggregatesAsync(pageItems.Select(a => a.Id).ToList());

        return new PagedResult<AgentResponse>
        {
            Items = pageItems.Select(a => MapToAgent(a, ratings)).ToList(),
            TotalCount = filtered.Count,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AgentResponse?> GetMyProfileAsync(string userId)
    {
        var agent = (await _agentRepository.FindAsync(a => a.UserId == userId)).FirstOrDefault();
        if (agent is null)
            return null;

        var ratings = await GetRatingAggregatesAsync(new List<string> { agent.Id });
        return MapToAgent(agent, ratings);
    }

    public async Task<AgentResponse> UpsertMyProfileAsync(string userId, UpsertAgentProfileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LicenseNumber))
            throw new ValidationException("A licence number is required");
        if (string.IsNullOrWhiteSpace(request.Bio))
            throw new ValidationException("A short bio is required");
        if (request.CommissionRate is < 0 or > 100)
            throw new ValidationException("Commission rate must be between 0 and 100");
        if (request.YearsOfExperience is < 0 or > 60)
            throw new ValidationException("Years of experience must be between 0 and 60");

        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("User");

        var phone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? user.Phone : request.PhoneNumber!;

        var agent = (await _agentRepository.FindAsync(a => a.UserId == userId)).FirstOrDefault();
        if (agent is null)
        {
            agent = new Agent
            {
                UserId = userId,
                LicenseNumber = request.LicenseNumber,
                PhoneNumber = phone,
                Bio = request.Bio,
                Status = AgentStatus.Active,
                CommissionRate = request.CommissionRate,
                YearsOfExperience = request.YearsOfExperience,
                Certifications = request.Certifications,
                ServiceArea = request.ServiceArea
            };
            await _agentRepository.AddAsync(agent);
            _logger.LogInformation("Agent directory profile created for user {UserId}", userId);
        }
        else
        {
            // Deliberately do NOT touch Status here: a Suspended agent must not be able to
            // re-activate themselves by resubmitting their profile.
            agent.LicenseNumber = request.LicenseNumber;
            agent.PhoneNumber = phone;
            agent.Bio = request.Bio;
            agent.CommissionRate = request.CommissionRate;
            agent.YearsOfExperience = request.YearsOfExperience;
            agent.Certifications = request.Certifications;
            agent.ServiceArea = request.ServiceArea;
            await _agentRepository.UpdateAsync(agent);
        }

        await _agentRepository.SaveChangesAsync();

        var ratings = await GetRatingAggregatesAsync(new List<string> { agent.Id });
        return MapToAgent(agent, ratings);
    }

    public async Task<AgentResponse?> GetAgentProfileAsync(string agentId)
    {
        var agent = await _agentRepository.GetByIdAsync(agentId);
        if (agent == null)
            return null;

        var ratings = await GetRatingAggregatesAsync(new List<string> { agent.Id });
        return MapToAgent(agent, ratings);
    }

    public async Task<ViewingRequestResponse> CreateViewingRequestAsync(
        string agentId,
        string propertyId,
        DateTime scheduledAt,
        string tenantId,
        string? notes)
    {
        var agent = await _agentRepository.GetByIdAsync(agentId)
            ?? throw new NotFoundException("Agent");
        if (agent.Status != AgentStatus.Active)
            throw new ValidationException("This agent is not currently taking viewing requests");

        var property = await _propertyRepository.GetByIdAsync(propertyId)
            ?? throw new NotFoundException("Property");

        if (scheduledAt <= DateTime.UtcNow)
            throw new ValidationException("The viewing must be scheduled in the future");

        var viewingRequest = new ViewingRequest
        {
            AgentId = agentId,
            PropertyId = propertyId,
            TenantId = tenantId,
            ScheduledAt = scheduledAt,
            Notes = notes,
            Status = ViewingRequestStatus.Pending
        };

        await _viewingRequestRepository.AddAsync(viewingRequest);
        await _viewingRequestRepository.SaveChangesAsync();

        _logger.LogInformation("Viewing request created: {ViewingRequestId}", viewingRequest.Id);

        await _notificationService.NotifyAsync(agent.UserId, NotificationType.ViewingRequestUpdate,
            "New viewing request",
            $"A tenant requested a viewing of \"{property.Title}\" on {scheduledAt:yyyy-MM-dd HH:mm} UTC.");

        return MapToViewingRequest(viewingRequest);
    }

    public async Task UpdateViewingRequestStatusAsync(string requestId, string status, string userId)
    {
        if (!Enum.TryParse<ViewingRequestStatus>(status, ignoreCase: true, out var parsedStatus))
            throw new ValidationException($"Invalid status value: {status}");

        await TransitionViewingRequestAsync(requestId, parsedStatus, userId, requireAgent: false);
    }

    public Task DeclineViewingRequestAsync(string requestId, string userId) =>
        TransitionViewingRequestAsync(requestId, ViewingRequestStatus.Declined, userId, requireAgent: true);

    private async Task TransitionViewingRequestAsync(
        string requestId, ViewingRequestStatus target, string userId, bool requireAgent)
    {
        var viewingRequest = await _viewingRequestRepository.GetByIdAsync(requestId)
            ?? throw new NotFoundException("Viewing request");

        // TenantId is a user id, but AgentId is the Agent *entity* id — resolve the caller's agent
        // profile so an agent can act on their own request (not just the tenant).
        var myAgent = await _agentRepository.GetByUserIdAsync(userId);
        var isAgent = myAgent is not null && viewingRequest.AgentId == myAgent.Id;
        var isTenant = viewingRequest.TenantId == userId;

        if (requireAgent && !isAgent)
            throw new ForbiddenException("This viewing request was not assigned to you");
        if (!isAgent && !isTenant)
            throw new ForbiddenException("You are not authorized to update this viewing request");

        var allowed = (isAgent && AgentTransitions.TryGetValue(viewingRequest.Status, out var a) && a.Contains(target))
                   || (isTenant && TenantTransitions.TryGetValue(viewingRequest.Status, out var t) && t.Contains(target));
        if (!allowed)
            throw new ValidationException(
                $"Cannot change this viewing request from {viewingRequest.Status} to {target}");

        viewingRequest.Status = target;

        await _viewingRequestRepository.UpdateAsync(viewingRequest);
        await _viewingRequestRepository.SaveChangesAsync();

        _logger.LogInformation("Viewing request {RequestId} status updated to {Status}", requestId, target);

        await NotifyViewingCounterpartyAsync(viewingRequest, actorIsAgent: isAgent,
            $"Your viewing request is now {target}.");
    }

    public async Task SubmitViewingReviewAsync(string requestId, string userId, int rating, string? comment)
    {
        if (rating is < 1 or > 5)
            throw new ValidationException("Rating must be between 1 and 5");

        var viewingRequest = await _viewingRequestRepository.GetByIdAsync(requestId)
            ?? throw new NotFoundException("Viewing request");

        // Only the requesting tenant may review the agent.
        if (viewingRequest.TenantId != userId)
            throw new ForbiddenException("Only the requesting tenant can review this viewing");

        if (viewingRequest.Status != ViewingRequestStatus.Completed)
            throw new ValidationException("Reviews can only be submitted for completed viewings");

        viewingRequest.Rating = rating;
        viewingRequest.ReviewComment = comment;

        await _viewingRequestRepository.UpdateAsync(viewingRequest);
        await _viewingRequestRepository.SaveChangesAsync();

        _logger.LogInformation("Review submitted for viewing request {RequestId} by user {UserId}", requestId, userId);

        await NotifyViewingCounterpartyAsync(viewingRequest, actorIsAgent: false,
            $"You received a {rating}-star review for a completed viewing.");
    }

    public async Task<List<ViewingRequestResponse>> GetMyViewingRequestsAsync(string userId)
    {
        // The caller may be the requesting tenant and/or an assigned agent.
        var agent = await _agentRepository.GetByUserIdAsync(userId);
        var agentId = agent?.Id;
        var requests = await _viewingRequestRepository.FindAsync(
            v => v.TenantId == userId || (agentId != null && v.AgentId == agentId));

        return requests
            .OrderByDescending(v => v.ScheduledAt)
            .Select(MapToViewingRequest)
            .ToList();
    }

    /// <summary>Notifies the party who did NOT perform the action (tenant ↔ agent's user).</summary>
    private async Task NotifyViewingCounterpartyAsync(ViewingRequest viewingRequest, bool actorIsAgent, string body)
    {
        string? recipient;
        if (actorIsAgent)
        {
            recipient = viewingRequest.TenantId;
        }
        else
        {
            var agent = await _agentRepository.GetByIdAsync(viewingRequest.AgentId);
            recipient = agent?.UserId;
        }

        if (!string.IsNullOrEmpty(recipient))
            await _notificationService.NotifyAsync(recipient, NotificationType.ViewingRequestUpdate,
                "Viewing request update", body);
    }

    private async Task<Dictionary<string, (double Average, int Count)>> GetRatingAggregatesAsync(
        List<string> agentIds)
    {
        if (agentIds.Count == 0)
            return new Dictionary<string, (double, int)>();

        var rated = await _viewingRequestRepository.FindAsync(
            v => v.Rating != null && agentIds.Contains(v.AgentId));

        return rated
            .GroupBy(v => v.AgentId)
            .ToDictionary(g => g.Key, g => (g.Average(v => (double)v.Rating!.Value), g.Count()));
    }

    private static AgentResponse MapToAgent(Agent a, IReadOnlyDictionary<string, (double Average, int Count)> ratings)
    {
        var hasRating = ratings.TryGetValue(a.Id, out var rating);
        return new AgentResponse
        {
            AgentId = a.Id,
            UserId = a.UserId,
            LicenseNumber = a.LicenseNumber,
            PhoneNumber = a.PhoneNumber,
            Bio = a.Bio,
            Status = a.Status,
            CommissionRate = a.CommissionRate,
            YearsOfExperience = a.YearsOfExperience,
            JoinDate = a.JoinDate,
            Certifications = a.Certifications,
            ServiceArea = a.ServiceArea,
            AverageRating = hasRating ? Math.Round(rating.Average, 2) : null,
            ReviewCount = hasRating ? rating.Count : 0
        };
    }

    private static ViewingRequestResponse MapToViewingRequest(ViewingRequest v) => new()
    {
        ViewingRequestId = v.Id,
        AgentId = v.AgentId,
        TenantId = v.TenantId,
        PropertyId = v.PropertyId,
        ScheduledAt = v.ScheduledAt,
        Notes = v.Notes,
        Status = v.Status.ToString(),
        Rating = v.Rating,
        ReviewComment = v.ReviewComment,
        CreatedAt = v.CreatedAt
    };
}
