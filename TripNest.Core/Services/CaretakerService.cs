using TripNest.Core.DTOs.Caretakers;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Enums;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class CaretakerService : ICaretakerService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    // Who may move a service request from which status to which. Anything not listed is rejected,
    // so a requester can no longer mark their own request Accepted/Completed.
    private static readonly Dictionary<ServiceRequestStatus, ServiceRequestStatus[]> CaretakerTransitions = new()
    {
        [ServiceRequestStatus.Pending] = new[] { ServiceRequestStatus.Accepted, ServiceRequestStatus.Declined },
        [ServiceRequestStatus.Accepted] = new[] { ServiceRequestStatus.InProgress, ServiceRequestStatus.Completed },
        [ServiceRequestStatus.InProgress] = new[] { ServiceRequestStatus.Completed },
    };

    private static readonly Dictionary<ServiceRequestStatus, ServiceRequestStatus[]> RequesterTransitions = new()
    {
        [ServiceRequestStatus.Pending] = new[] { ServiceRequestStatus.Cancelled },
        [ServiceRequestStatus.Accepted] = new[] { ServiceRequestStatus.Cancelled },
    };

    private readonly ICaretakerRepository _caretakerRepository;
    private readonly IPropertyRepository _propertyRepository;
    private readonly IRepository<ServiceRequest> _serviceRequestRepository;
    private readonly IRepository<PropertyCaretakerAssignment> _assignmentRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<CaretakerService> _logger;

    public CaretakerService(
        ICaretakerRepository caretakerRepository,
        IPropertyRepository propertyRepository,
        IRepository<ServiceRequest> serviceRequestRepository,
        IRepository<PropertyCaretakerAssignment> assignmentRepository,
        INotificationService notificationService,
        ILogger<CaretakerService> logger)
    {
        _caretakerRepository = caretakerRepository;
        _propertyRepository = propertyRepository;
        _serviceRequestRepository = serviceRequestRepository;
        _assignmentRepository = assignmentRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<PagedResult<CaretakerResponse>> GetAvailableCaretakersAsync(
        string? serviceType, string? area, int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize is 0 ? DefaultPageSize : pageSize, 1, MaxPageSize);

        var active = (await _caretakerRepository.FindAsync(c => c.Status == CaretakerStatus.Active)).AsEnumerable();

        if (!string.IsNullOrWhiteSpace(serviceType))
            active = active.Where(c => c.Responsibilities.Contains(serviceType, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(area))
            active = active.Where(c => c.ServiceArea != null && c.ServiceArea.Contains(area, StringComparison.OrdinalIgnoreCase));

        var filtered = active.OrderByDescending(c => c.StartDate).ToList();
        var pageItems = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var ratings = await GetRatingAggregatesAsync(pageItems.Select(c => c.Id).ToList());

        return new PagedResult<CaretakerResponse>
        {
            Items = pageItems.Select(c => MapToCaretaker(c, ratings)).ToList(),
            TotalCount = filtered.Count,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CaretakerResponse?> GetCaretakerProfileAsync(string caretakerId)
    {
        var caretaker = await _caretakerRepository.GetByIdAsync(caretakerId);
        if (caretaker == null)
            return null;

        var ratings = await GetRatingAggregatesAsync(new List<string> { caretaker.Id });
        return MapToCaretaker(caretaker, ratings);
    }

    public async Task<CaretakerResponse?> GetMyProfileAsync(string userId)
    {
        var caretaker = (await _caretakerRepository.GetByUserIdAsync(userId)).FirstOrDefault();
        if (caretaker == null)
            return null;

        var ratings = await GetRatingAggregatesAsync(new List<string> { caretaker.Id });
        return MapToCaretaker(caretaker, ratings);
    }

    public async Task<CaretakerResponse> UpsertMyProfileAsync(string userId, UpsertCaretakerProfileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Responsibilities))
            throw new ValidationException("Responsibilities (services offered) are required");
        if (request.MonthlyCompensation is < 0)
            throw new ValidationException("Monthly compensation cannot be negative");

        var caretaker = (await _caretakerRepository.GetByUserIdAsync(userId)).FirstOrDefault();
        if (caretaker is null)
        {
            caretaker = new Caretaker
            {
                UserId = userId,
                Responsibilities = request.Responsibilities,
                Bio = request.Bio,
                ServiceArea = request.ServiceArea,
                MonthlyCompensation = request.MonthlyCompensation,
                Status = CaretakerStatus.Active
            };
            await _caretakerRepository.AddAsync(caretaker);
            _logger.LogInformation("Caretaker directory profile created for user {UserId}", userId);
        }
        else
        {
            // Deliberately do NOT touch Status here: a Suspended caretaker must not be able to
            // re-activate themselves by resubmitting their profile.
            caretaker.Responsibilities = request.Responsibilities;
            caretaker.Bio = request.Bio;
            caretaker.ServiceArea = request.ServiceArea;
            caretaker.MonthlyCompensation = request.MonthlyCompensation;
            await _caretakerRepository.UpdateAsync(caretaker);
        }

        await _caretakerRepository.SaveChangesAsync();

        var ratings = await GetRatingAggregatesAsync(new List<string> { caretaker.Id });
        return MapToCaretaker(caretaker, ratings);
    }

    public async Task AssignCaretakerToPropertyAsync(string propertyId, string caretakerId, string landlordId)
    {
        var property = await _propertyRepository.GetByIdAsync(propertyId)
            ?? throw new NotFoundException("Property");
        if (property.UserId != landlordId)
            throw new ForbiddenException("You are not authorised to assign a caretaker to this property");

        var caretaker = await _caretakerRepository.GetByIdAsync(caretakerId)
            ?? throw new NotFoundException("Caretaker");
        if (caretaker.Status != CaretakerStatus.Active)
            throw new ValidationException("This caretaker is not currently available for assignments");

        var activeAssignments = await _assignmentRepository.FindAsync(
            a => a.PropertyId == propertyId && a.CaretakerId == caretakerId && a.IsActive);
        if (activeAssignments.Any())
            throw new ConflictException("Caretaker is already assigned to this property");

        await _assignmentRepository.AddAsync(new PropertyCaretakerAssignment
        {
            PropertyId = propertyId,
            CaretakerId = caretakerId,
            AssignedByUserId = landlordId
        });
        await _assignmentRepository.SaveChangesAsync();

        _logger.LogInformation("Caretaker {CaretakerId} assigned to property {PropertyId}", caretakerId, propertyId);

        await _notificationService.NotifyAsync(caretaker.UserId, NotificationType.General,
            "New property assignment",
            $"You have been assigned as caretaker of \"{property.Title}\".");
    }

    public async Task UnassignCaretakerFromPropertyAsync(string propertyId, string caretakerId, string landlordId)
    {
        var property = await _propertyRepository.GetByIdAsync(propertyId)
            ?? throw new NotFoundException("Property");
        if (property.UserId != landlordId)
            throw new ForbiddenException("You are not authorised to manage caretakers on this property");

        var assignment = (await _assignmentRepository.FindAsync(
                a => a.PropertyId == propertyId && a.CaretakerId == caretakerId && a.IsActive))
            .FirstOrDefault()
            ?? throw new NotFoundException("Active caretaker assignment");

        assignment.IsActive = false;
        assignment.EndedAt = DateTime.UtcNow;
        await _assignmentRepository.UpdateAsync(assignment);
        await _assignmentRepository.SaveChangesAsync();

        _logger.LogInformation("Caretaker {CaretakerId} unassigned from property {PropertyId}", caretakerId, propertyId);

        var caretaker = await _caretakerRepository.GetByIdAsync(caretakerId);
        if (caretaker != null)
            await _notificationService.NotifyAsync(caretaker.UserId, NotificationType.General,
                "Assignment ended",
                $"Your caretaker assignment for \"{property.Title}\" has ended.");
    }

    public async Task<PagedResult<CaretakerAssignmentResponse>> GetMyAssignmentsAsync(string userId, int page, int pageSize)
    {
        var myPropertyIds = (await _propertyRepository.FindAsync(p => p.UserId == userId))
            .Select(p => p.Id).ToList();
        var myCaretakerIds = (await _caretakerRepository.GetByUserIdAsync(userId))
            .Select(c => c.Id).ToList();

        var assignments = await _assignmentRepository.FindAsync(
            a => myPropertyIds.Contains(a.PropertyId) || myCaretakerIds.Contains(a.CaretakerId));

        return Paging.Page(assignments
            .OrderByDescending(a => a.AssignedAt)
            .Select(a => new CaretakerAssignmentResponse
            {
                AssignmentId = a.Id,
                PropertyId = a.PropertyId,
                CaretakerId = a.CaretakerId,
                AssignedByUserId = a.AssignedByUserId,
                IsActive = a.IsActive,
                AssignedAt = a.AssignedAt,
                EndedAt = a.EndedAt
            })
            .ToList(), page, pageSize);
    }

    public async Task<ServiceRequestResponse> CreateServiceRequestAsync(CreateServiceRequestRequest request, string userId)
    {
        if (string.IsNullOrWhiteSpace(request.CaretakerId))
            throw new ValidationException("CaretakerId is required");

        var caretaker = await _caretakerRepository.GetByIdAsync(request.CaretakerId)
            ?? throw new NotFoundException("Caretaker");
        if (caretaker.Status != CaretakerStatus.Active)
            throw new ValidationException("This caretaker is not currently taking service requests");

        // Fall back to the caretaker's single active assignment (or legacy property link) when the
        // requester doesn't name a property explicitly.
        var propertyId = request.PropertyId;
        if (!string.IsNullOrWhiteSpace(propertyId))
        {
            _ = await _propertyRepository.GetByIdAsync(propertyId)
                ?? throw new NotFoundException("Property");
        }
        else
        {
            var activeAssignments = await _assignmentRepository.FindAsync(
                a => a.CaretakerId == caretaker.Id && a.IsActive);
            var assignedPropertyIds = activeAssignments.Select(a => a.PropertyId).Distinct().ToList();
            propertyId = assignedPropertyIds.Count == 1
                ? assignedPropertyIds[0]
                : caretaker.PropertyId;
        }
        if (string.IsNullOrWhiteSpace(propertyId))
            throw new ValidationException("PropertyId is required when the caretaker serves more than one property");

        var serviceRequest = new ServiceRequest
        {
            CaretakerId = caretaker.Id,
            RequestedByUserId = userId,
            PropertyId = propertyId,
            ServiceType = request.ServiceType,
            Description = request.Description
        };

        await _serviceRequestRepository.AddAsync(serviceRequest);
        await _serviceRequestRepository.SaveChangesAsync();

        _logger.LogInformation("Service request {ServiceRequestId} created by user {UserId}", serviceRequest.Id, userId);

        await _notificationService.NotifyAsync(caretaker.UserId, NotificationType.ServiceRequestUpdate,
            "New service request",
            $"You have a new {request.ServiceType} request awaiting your response.");

        return MapToServiceRequest(serviceRequest);
    }

    public async Task<PagedResult<ServiceRequestResponse>> GetServiceRequestsAsync(string userId, int page, int pageSize)
    {
        // A caller sees requests they raised (RequestedByUserId) plus requests assigned to any
        // caretaker profile they own. ServiceRequest.CaretakerId is the Caretaker *entity* id, not
        // the user id, so resolve the caller's caretaker ids first.
        var myCaretakerIds = (await _caretakerRepository.GetByUserIdAsync(userId)).Select(c => c.Id).ToList();
        var requests = await _serviceRequestRepository.FindAsync(
            s => s.RequestedByUserId == userId || myCaretakerIds.Contains(s.CaretakerId));

        return Paging.Page(requests
            .OrderByDescending(s => s.CreatedAt)
            .Select(MapToServiceRequest)
            .ToList(), page, pageSize);
    }

    public Task AcceptServiceRequestAsync(string requestId, string userId) =>
        TransitionServiceRequestAsync(requestId, ServiceRequestStatus.Accepted, userId, requireCaretaker: true);

    public Task DeclineServiceRequestAsync(string requestId, string userId) =>
        TransitionServiceRequestAsync(requestId, ServiceRequestStatus.Declined, userId, requireCaretaker: true);

    public async Task UpdateServiceRequestStatusAsync(string requestId, string status, string userId)
    {
        if (!Enum.TryParse<ServiceRequestStatus>(status, ignoreCase: true, out var parsedStatus))
            throw new ValidationException($"Invalid status value: {status}");

        await TransitionServiceRequestAsync(requestId, parsedStatus, userId, requireCaretaker: false);
    }

    private async Task TransitionServiceRequestAsync(
        string requestId, ServiceRequestStatus target, string userId, bool requireCaretaker)
    {
        var serviceRequest = await _serviceRequestRepository.GetByIdAsync(requestId)
            ?? throw new NotFoundException("Service request");

        // The caller may act as the requester and/or as a caretaker: match the request's assigned
        // Caretaker entity against the caretaker profile(s) this user owns (Caretaker.Id != User.Id).
        var myCaretakerIds = (await _caretakerRepository.GetByUserIdAsync(userId)).Select(c => c.Id).ToHashSet();
        var isCaretaker = myCaretakerIds.Contains(serviceRequest.CaretakerId);
        var isRequester = serviceRequest.RequestedByUserId == userId;

        if (requireCaretaker && !isCaretaker)
            throw new ForbiddenException("This service request was not assigned to you");
        if (!isCaretaker && !isRequester)
            throw new ForbiddenException("You are not authorised to update this service request");

        var allowed = (isCaretaker && CaretakerTransitions.TryGetValue(serviceRequest.Status, out var c) && c.Contains(target))
                   || (isRequester && RequesterTransitions.TryGetValue(serviceRequest.Status, out var r) && r.Contains(target));
        if (!allowed)
            throw new ValidationException(
                $"Cannot change this service request from {serviceRequest.Status} to {target}");

        serviceRequest.Status = target;
        if (target == ServiceRequestStatus.Completed)
            serviceRequest.CompletedAt = DateTime.UtcNow;

        await _serviceRequestRepository.UpdateAsync(serviceRequest);
        await _serviceRequestRepository.SaveChangesAsync();

        _logger.LogInformation("Service request {RequestId} status updated to {Status} by user {UserId}",
            requestId, target, userId);

        await NotifyServiceRequestCounterpartyAsync(serviceRequest, actorIsCaretaker: isCaretaker,
            $"Your service request is now {target}.");
    }

    public async Task SubmitServiceReviewAsync(string requestId, string userId, int rating, string? comment)
    {
        if (rating is < 1 or > 5)
            throw new ValidationException("Rating must be between 1 and 5");

        var serviceRequest = await _serviceRequestRepository.GetByIdAsync(requestId)
            ?? throw new NotFoundException("Service request");

        // Only the requester (the person who asked for the service) may review it.
        if (serviceRequest.RequestedByUserId != userId)
            throw new ForbiddenException("Only the requester can review this service request");

        if (serviceRequest.Status != ServiceRequestStatus.Completed)
            throw new ValidationException("Reviews can only be submitted for completed service requests");

        serviceRequest.Rating = rating;
        serviceRequest.ReviewComment = comment;

        await _serviceRequestRepository.UpdateAsync(serviceRequest);
        await _serviceRequestRepository.SaveChangesAsync();

        _logger.LogInformation("Review submitted for service request {RequestId} by user {UserId}", requestId, userId);

        await NotifyServiceRequestCounterpartyAsync(serviceRequest, actorIsCaretaker: false,
            $"You received a {rating}-star review for a completed service request.");
    }

    /// <summary>Notifies the party who did NOT perform the action (requester ↔ caretaker's user).</summary>
    private async Task NotifyServiceRequestCounterpartyAsync(
        ServiceRequest serviceRequest, bool actorIsCaretaker, string body)
    {
        string? recipient;
        if (actorIsCaretaker)
        {
            recipient = serviceRequest.RequestedByUserId;
        }
        else
        {
            var caretaker = await _caretakerRepository.GetByIdAsync(serviceRequest.CaretakerId);
            recipient = caretaker?.UserId;
        }

        if (!string.IsNullOrEmpty(recipient))
            await _notificationService.NotifyAsync(recipient, NotificationType.ServiceRequestUpdate,
                "Service request update", body);
    }

    private async Task<Dictionary<string, (double Average, int Count)>> GetRatingAggregatesAsync(
        List<string> caretakerIds)
    {
        if (caretakerIds.Count == 0)
            return new Dictionary<string, (double, int)>();

        var rated = await _serviceRequestRepository.FindAsync(
            s => s.Rating != null && caretakerIds.Contains(s.CaretakerId));

        return rated
            .GroupBy(s => s.CaretakerId)
            .ToDictionary(g => g.Key, g => (g.Average(s => (double)s.Rating!.Value), g.Count()));
    }

    private static CaretakerResponse MapToCaretaker(
        Caretaker c, IReadOnlyDictionary<string, (double Average, int Count)> ratings)
    {
        var hasRating = ratings.TryGetValue(c.Id, out var rating);
        return new CaretakerResponse
        {
            CaretakerId = c.Id,
            UserId = c.UserId,
            PropertyId = c.PropertyId,
            Status = c.Status,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            MonthlyCompensation = c.MonthlyCompensation,
            Responsibilities = c.Responsibilities,
            Bio = c.Bio,
            ServiceArea = c.ServiceArea,
            AverageRating = hasRating ? Math.Round(rating.Average, 2) : null,
            ReviewCount = hasRating ? rating.Count : 0
        };
    }

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
