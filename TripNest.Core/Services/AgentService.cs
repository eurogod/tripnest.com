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
    private readonly ILogger<AgentService> _logger;

    public AgentService(
        IAgentRepository agentRepository,
        IPropertyRepository propertyRepository,
        IRepository<ViewingRequest> viewingRequestRepository,
        ILogger<AgentService> logger)
    {
        _agentRepository = agentRepository;
        _propertyRepository = propertyRepository;
        _viewingRequestRepository = viewingRequestRepository;
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
