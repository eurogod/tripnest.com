using TripNest.Core.DTOs.Caretakers;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class CaretakerService : ICaretakerService
{
    private readonly ICaretakerRepository _caretakerRepository;
    private readonly IPropertyRepository _propertyRepository;
    private readonly IRepository<ServiceRequest> _serviceRequestRepository;
    private readonly IRepository<PropertyCaretakerAssignment> _assignmentRepository;
    private readonly ILogger<CaretakerService> _logger;

    public CaretakerService(
        ICaretakerRepository caretakerRepository,
        IPropertyRepository propertyRepository,
        IRepository<ServiceRequest> serviceRequestRepository,
        IRepository<PropertyCaretakerAssignment> assignmentRepository,
        ILogger<CaretakerService> logger)
    {
        _caretakerRepository = caretakerRepository;
        _propertyRepository = propertyRepository;
        _serviceRequestRepository = serviceRequestRepository;
        _assignmentRepository = assignmentRepository;
        _logger = logger;
    }

    public async Task<List<CaretakerResponse>> GetAvailableCaretakersAsync(string? serviceType, string? area)
    {
        try
        {
            var caretakers = await _caretakerRepository.GetAllAsync();

            var active = caretakers.Where(c => c.Status == CaretakerStatus.Active);

            if (!string.IsNullOrWhiteSpace(serviceType))
                active = active.Where(c => c.Responsibilities.Contains(serviceType, StringComparison.OrdinalIgnoreCase));

            return active.Select(MapToCaretaker).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available caretakers");
            throw;
        }
    }

    public async Task<CaretakerResponse?> GetCaretakerProfileAsync(string caretakerId)
    {
        try
        {
            var caretaker = await _caretakerRepository.GetByIdAsync(caretakerId);
            if (caretaker == null)
                return null;

            return MapToCaretaker(caretaker);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving caretaker profile for {CaretakerId}", caretakerId);
            throw;
        }
    }

    public async Task AssignCaretakerToPropertyAsync(string propertyId, string caretakerId, string landlordId)
    {
        try
        {
            var property = await _propertyRepository.GetByIdAsync(propertyId);
            if (property == null)
                throw new InvalidOperationException("Property not found");

            if (property.UserId != landlordId)
                throw new InvalidOperationException("You are not authorised to assign a caretaker to this property");

            var caretaker = await _caretakerRepository.GetByIdAsync(caretakerId);
            if (caretaker == null)
                throw new InvalidOperationException("Caretaker not found");

            var existing = await _caretakerRepository.GetByPropertyIdAsync(propertyId);
            if (existing.Any(c => c.Id == caretakerId))
                throw new InvalidOperationException("Caretaker is already assigned to this property");

            caretaker.PropertyId = propertyId;

            await _caretakerRepository.UpdateAsync(caretaker);
            await _caretakerRepository.SaveChangesAsync();

            // Record the assignment (who assigned, when) as a first-class history entry.
            await _assignmentRepository.AddAsync(new PropertyCaretakerAssignment
            {
                PropertyId = propertyId,
                CaretakerId = caretakerId,
                AssignedByUserId = landlordId
            });
            await _assignmentRepository.SaveChangesAsync();

            _logger.LogInformation("Caretaker {CaretakerId} assigned to property {PropertyId}", caretakerId, propertyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning caretaker {CaretakerId} to property {PropertyId}", caretakerId, propertyId);
            throw;
        }
    }

    public async Task<ServiceRequestResponse> CreateServiceRequestAsync(CreateServiceRequestRequest request, string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.CaretakerId))
                throw new InvalidOperationException("CaretakerId is required");

            var caretaker = await _caretakerRepository.GetByIdAsync(request.CaretakerId);
            if (caretaker == null)
                throw new InvalidOperationException("Caretaker not found");

            var propertyId = request.PropertyId ?? caretaker.PropertyId;

            var serviceRequest = new ServiceRequest
            {
                CaretakerId = request.CaretakerId,
                RequestedByUserId = userId,
                PropertyId = propertyId,
                ServiceType = request.ServiceType,
                Description = request.Description
            };

            await _serviceRequestRepository.AddAsync(serviceRequest);
            await _serviceRequestRepository.SaveChangesAsync();

            _logger.LogInformation("Service request {ServiceRequestId} created by user {UserId}", serviceRequest.Id, userId);

            return MapToServiceRequest(serviceRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating service request for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<ServiceRequestResponse>> GetServiceRequestsAsync(string userId)
    {
        try
        {
            var all = await _serviceRequestRepository.GetAllAsync();

            return all
                .Where(s => s.RequestedByUserId == userId || s.CaretakerId == userId)
                .Select(MapToServiceRequest)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving service requests for user {UserId}", userId);
            throw;
        }
    }

    public async Task AcceptServiceRequestAsync(string requestId, string caretakerId)
    {
        try
        {
            var serviceRequest = await _serviceRequestRepository.GetByIdAsync(requestId);
            if (serviceRequest == null)
                throw new InvalidOperationException("Service request not found");

            var caretaker = await _caretakerRepository.GetByIdAsync(caretakerId);
            if (caretaker == null)
                throw new InvalidOperationException("Caretaker not found");

            if (serviceRequest.CaretakerId != caretakerId)
                throw new InvalidOperationException("This service request was not assigned to you");

            serviceRequest.Status = ServiceRequestStatus.Accepted;

            await _serviceRequestRepository.UpdateAsync(serviceRequest);
            await _serviceRequestRepository.SaveChangesAsync();

            _logger.LogInformation("Service request {RequestId} accepted by caretaker {CaretakerId}", requestId, caretakerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting service request {RequestId}", requestId);
            throw;
        }
    }

    public async Task UpdateServiceRequestStatusAsync(string requestId, string status, string userId)
    {
        try
        {
            var serviceRequest = await _serviceRequestRepository.GetByIdAsync(requestId);
            if (serviceRequest == null)
                throw new InvalidOperationException("Service request not found");

            if (!Enum.TryParse<ServiceRequestStatus>(status, ignoreCase: true, out var parsedStatus))
                throw new InvalidOperationException($"Invalid status value: {status}");

            serviceRequest.Status = parsedStatus;

            if (parsedStatus == ServiceRequestStatus.Completed)
                serviceRequest.CompletedAt = DateTime.UtcNow;

            await _serviceRequestRepository.UpdateAsync(serviceRequest);
            await _serviceRequestRepository.SaveChangesAsync();

            _logger.LogInformation("Service request {RequestId} status updated to {Status} by user {UserId}", requestId, parsedStatus, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating service request {RequestId} status", requestId);
            throw;
        }
    }

    public async Task SubmitServiceReviewAsync(string requestId, string userId, int rating, string? comment)
    {
        try
        {
            var serviceRequest = await _serviceRequestRepository.GetByIdAsync(requestId);
            if (serviceRequest == null)
                throw new InvalidOperationException("Service request not found");

            if (serviceRequest.Status != ServiceRequestStatus.Completed)
                throw new InvalidOperationException("Reviews can only be submitted for completed service requests");

            serviceRequest.Rating = rating;
            serviceRequest.ReviewComment = comment;

            await _serviceRequestRepository.UpdateAsync(serviceRequest);
            await _serviceRequestRepository.SaveChangesAsync();

            _logger.LogInformation("Review submitted for service request {RequestId} by user {UserId}", requestId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting review for service request {RequestId}", requestId);
            throw;
        }
    }

    private static CaretakerResponse MapToCaretaker(Caretaker c) => new()
    {
        CaretakerId = c.Id,
        UserId = c.UserId,
        PropertyId = c.PropertyId,
        Status = c.Status,
        StartDate = c.StartDate,
        EndDate = c.EndDate,
        MonthlyCompensation = c.MonthlyCompensation,
        Responsibilities = c.Responsibilities
    };

    private static ServiceRequestResponse MapToServiceRequest(ServiceRequest s) => new()
    {
        ServiceRequestId = s.Id,
        CaretakerId = s.CaretakerId,
        RequestedByUserId = s.RequestedByUserId,
        PropertyId = s.PropertyId,
        ServiceType = s.ServiceType,
        Description = s.Description,
        Status = s.Status.ToString(),
        Rating = s.Rating,
        ReviewComment = s.ReviewComment,
        CreatedAt = s.CreatedAt,
        CompletedAt = s.CompletedAt
    };
}
