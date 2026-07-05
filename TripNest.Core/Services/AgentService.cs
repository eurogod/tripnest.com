using TripNest.Core.DTOs.Agents;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class AgentService : IAgentService
{
    private readonly IAgentRepository _agentRepository;
    private readonly IPropertyRepository _propertyRepository;
    private readonly IRepository<ViewingRequest> _viewingRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<AgentService> _logger;

    public AgentService(
        IAgentRepository agentRepository,
        IPropertyRepository propertyRepository,
        IRepository<ViewingRequest> viewingRequestRepository,
        IUserRepository userRepository,
        ILogger<AgentService> logger)
    {
        _agentRepository = agentRepository;
        _propertyRepository = propertyRepository;
        _viewingRequestRepository = viewingRequestRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<List<AgentResponse>> GetVerifiedAgentsAsync(string? serviceArea)
    {
        try
        {
            var activeAgents = await _agentRepository.FindAsync(a => a.Status == AgentStatus.Active);

            // serviceArea filtering is a future feature — no area field on Agent yet
            return activeAgents.Select(MapToAgent).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving verified agents");
            throw;
        }
    }

    public async Task<AgentResponse?> GetMyProfileAsync(string userId)
    {
        var agent = (await _agentRepository.FindAsync(a => a.UserId == userId)).FirstOrDefault();
        return agent is null ? null : MapToAgent(agent);
    }

    public async Task<AgentResponse> UpsertMyProfileAsync(string userId, UpsertAgentProfileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LicenseNumber))
            throw new InvalidOperationException("A licence number is required");
        if (string.IsNullOrWhiteSpace(request.Bio))
            throw new InvalidOperationException("A short bio is required");
        if (request.CommissionRate is < 0 or > 100)
            throw new InvalidOperationException("Commission rate must be between 0 and 100");
        if (request.YearsOfExperience is < 0 or > 60)
            throw new InvalidOperationException("Years of experience must be between 0 and 60");

        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found");

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
                Certifications = request.Certifications
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
            await _agentRepository.UpdateAsync(agent);
        }

        await _agentRepository.SaveChangesAsync();
        return MapToAgent(agent);
    }

    public async Task<AgentResponse?> GetAgentProfileAsync(string agentId)
    {
        try
        {
            var agent = await _agentRepository.GetByIdAsync(agentId);
            if (agent == null)
                return null;

            return MapToAgent(agent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agent profile for {AgentId}", agentId);
            throw;
        }
    }

    public async Task<ViewingRequestResponse> CreateViewingRequestAsync(
        string agentId,
        string propertyId,
        DateTime scheduledAt,
        string tenantId,
        string? notes)
    {
        try
        {
            var agent = await _agentRepository.GetByIdAsync(agentId);
            if (agent == null)
                throw new InvalidOperationException("Agent not found");

            var property = await _propertyRepository.GetByIdAsync(propertyId);
            if (property == null)
                throw new InvalidOperationException("Property not found");

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

            return MapToViewingRequest(viewingRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating viewing request");
            throw;
        }
    }

    public async Task UpdateViewingRequestStatusAsync(string requestId, string status, string userId)
    {
        try
        {
            var viewingRequest = await _viewingRequestRepository.GetByIdAsync(requestId);
            if (viewingRequest == null)
                throw new InvalidOperationException("Viewing request not found");

            // TenantId is a user id, but AgentId is the Agent *entity* id — resolve the caller's agent
            // profile so an agent can act on their own request (not just the tenant).
            var myAgent = await _agentRepository.GetByUserIdAsync(userId);
            var callerIsAssignedAgent = myAgent is not null && viewingRequest.AgentId == myAgent.Id;
            if (viewingRequest.TenantId != userId && !callerIsAssignedAgent)
                throw new UnauthorizedAccessException("You are not authorized to update this viewing request");

            if (!Enum.TryParse<ViewingRequestStatus>(status, ignoreCase: true, out var parsedStatus))
                throw new InvalidOperationException($"Invalid status value: {status}");

            viewingRequest.Status = parsedStatus;

            await _viewingRequestRepository.UpdateAsync(viewingRequest);
            await _viewingRequestRepository.SaveChangesAsync();

            _logger.LogInformation("Viewing request {RequestId} status updated to {Status}", requestId, parsedStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating viewing request status for {RequestId}", requestId);
            throw;
        }
    }

    public async Task<List<ViewingRequestResponse>> GetMyViewingRequestsAsync(string userId)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving viewing requests for user {UserId}", userId);
            throw;
        }
    }

    private static AgentResponse MapToAgent(Agent a) => new AgentResponse
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
        Certifications = a.Certifications
    };

    private static ViewingRequestResponse MapToViewingRequest(ViewingRequest v) => new ViewingRequestResponse
    {
        ViewingRequestId = v.Id,
        AgentId = v.AgentId,
        TenantId = v.TenantId,
        PropertyId = v.PropertyId,
        ScheduledAt = v.ScheduledAt,
        Notes = v.Notes,
        Status = v.Status.ToString(),
        CreatedAt = v.CreatedAt
    };
}
