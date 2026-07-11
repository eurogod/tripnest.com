using TripNest.Core.DTOs.Caretakers;
using TripNest.Core.DTOs.Maintenance;
using TripNest.Core.Enums;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class MaintenanceService : IMaintenanceService
{
    private readonly IMaintenanceRepository _maintenanceRepository;
    private readonly IPropertyRepository _propertyRepository;
    private readonly IRepository<ServiceRequest> _serviceRequestRepository;
    private readonly ILogger<MaintenanceService> _logger;

    public MaintenanceService(
        IMaintenanceRepository maintenanceRepository,
        IPropertyRepository propertyRepository,
        IRepository<ServiceRequest> serviceRequestRepository,
        ILogger<MaintenanceService> logger)
    {
        _maintenanceRepository = maintenanceRepository;
        _propertyRepository = propertyRepository;
        _serviceRequestRepository = serviceRequestRepository;
        _logger = logger;
    }

    public async Task<MaintenanceResponse> ReportMaintenanceAsync(CreateMaintenanceRequest request, string tenantId)
    {
        var property = await _propertyRepository.GetByIdAsync(request.PropertyId);
        if (property == null)
            throw new NotFoundException("Property");

        var maintenance = new Maintenance
        {
            PropertyId = request.PropertyId,
            ReportedByUserId = tenantId,
            Description = request.Description,
            Status = MaintenanceStatus.Reported
        };

        await _maintenanceRepository.AddAsync(maintenance);
        await _maintenanceRepository.SaveChangesAsync();

        _logger.LogInformation("Maintenance reported: {MaintenanceId} for property {PropertyId}", maintenance.Id, maintenance.PropertyId);

        return MapToResponse(maintenance);
    }

    public async Task<List<MaintenanceResponse>> GetPropertyMaintenanceAsync(string propertyId, string landlordId)
    {
        var property = await _propertyRepository.GetByIdAsync(propertyId);
        if (property == null)
            throw new NotFoundException("Property");

        if (property.UserId != landlordId)
            throw new ForbiddenException("You do not have permission to view maintenance for this property");

        var records = await _maintenanceRepository.GetByPropertyIdAsync(propertyId);
        return records.Select(MapToResponse).ToList();
    }

    public async Task<List<MaintenanceResponse>> GetTenantMaintenanceAsync(string tenantId)
    {
        var records = await _maintenanceRepository.GetByUserIdAsync(tenantId);
        return records.Select(MapToResponse).ToList();
    }

    public async Task UpdateMaintenanceStatusAsync(string maintenanceId, string status, string userId, bool isAdmin)
    {
        var maintenance = await _maintenanceRepository.GetByIdAsync(maintenanceId);
        if (maintenance == null)
            throw new NotFoundException("Maintenance record");

        // Only the property's owner (landlord) — or an admin — may change a maintenance status.
        // Without this any authenticated user could update any property's maintenance record.
        if (!isAdmin)
        {
            var property = await _propertyRepository.GetByIdAsync(maintenance.PropertyId);
            if (property == null || property.UserId != userId)
                throw new ForbiddenException("You do not have permission to update this maintenance record");
        }

        if (!Enum.TryParse<MaintenanceStatus>(status, ignoreCase: true, out var parsedStatus))
            throw new ValidationException($"Invalid maintenance status: {status}");

        maintenance.Status = parsedStatus;

        if (parsedStatus == MaintenanceStatus.Completed)
            maintenance.CompletedAt = DateTime.UtcNow;

        await _maintenanceRepository.UpdateAsync(maintenance);
        await _maintenanceRepository.SaveChangesAsync();

        _logger.LogInformation("Maintenance {MaintenanceId} status updated to {Status} by user {UserId}", maintenanceId, parsedStatus, userId);
    }

    public async Task<ServiceRequestResponse> ConvertToServiceRequestAsync(string maintenanceId, string? caretakerId, string landlordId)
    {
        var maintenance = await _maintenanceRepository.GetByIdAsync(maintenanceId);
        if (maintenance == null)
            throw new NotFoundException("Maintenance record");

        var property = await _propertyRepository.GetByIdAsync(maintenance.PropertyId);
        if (property == null)
            throw new NotFoundException("Property");

        if (property.UserId != landlordId)
            throw new ForbiddenException("You do not have permission to convert this maintenance record");

        var serviceRequest = new ServiceRequest
        {
            CaretakerId = caretakerId ?? "unassigned",
            RequestedByUserId = landlordId,
            PropertyId = maintenance.PropertyId,
            ServiceType = "Maintenance",
            Description = maintenance.Description
        };

        await _serviceRequestRepository.AddAsync(serviceRequest);

        maintenance.Status = MaintenanceStatus.Assigned;
        await _maintenanceRepository.UpdateAsync(maintenance);

        await _serviceRequestRepository.SaveChangesAsync();

        _logger.LogInformation("Maintenance {MaintenanceId} converted to service request {ServiceRequestId}", maintenanceId, serviceRequest.Id);

        return new ServiceRequestResponse
        {
            ServiceRequestId = serviceRequest.Id,
            CaretakerId = serviceRequest.CaretakerId,
            RequestedByUserId = serviceRequest.RequestedByUserId,
            PropertyId = serviceRequest.PropertyId,
            ServiceType = serviceRequest.ServiceType,
            Description = serviceRequest.Description,
            Status = serviceRequest.Status.ToString(),
            Rating = serviceRequest.Rating,
            ReviewComment = serviceRequest.ReviewComment,
            CreatedAt = serviceRequest.CreatedAt,
            CompletedAt = serviceRequest.CompletedAt
        };
    }

    private static MaintenanceResponse MapToResponse(Maintenance m)
    {
        return new MaintenanceResponse
        {
            MaintenanceId = m.Id,
            PropertyId = m.PropertyId,
            ReportedByUserId = m.ReportedByUserId,
            Description = m.Description,
            Status = m.Status,
            PhotoPath = m.PhotoPath,
            CreatedAt = m.CreatedAt,
            CompletedAt = m.CompletedAt,
            Resolution = m.Resolution
        };
    }
}
