namespace TripNest.Core.DTOs.Caretakers;

public class AssignCaretakerRequest
{
    public required string PropertyId { get; set; }
    public required string CaretakerId { get; set; }
}

/// <summary>
/// Self-service create/update of the caller's public caretaker directory profile. Without this
/// profile a Caretaker-role account never appears in <c>GET /api/caretakers</c> and cannot be
/// assigned to properties or take service requests.
/// </summary>
public class UpsertCaretakerProfileRequest
{
    /// <summary>Services offered, free text (e.g. "cleaning, plumbing, garden upkeep").</summary>
    public required string Responsibilities { get; set; }

    public string? Bio { get; set; }

    /// <summary>Area served (e.g. "Accra", "East Legon") — matched by the list filter.</summary>
    public string? ServiceArea { get; set; }

    /// <summary>Asking rate for permanent property assignments.</summary>
    public decimal? MonthlyCompensation { get; set; }
}

public class CreateServiceRequestRequest
{
    public string? PropertyId { get; set; }
    public string? CaretakerId { get; set; }
    public required string ServiceType { get; set; }
    public required string Description { get; set; }
    public DateTime? ScheduledFor { get; set; }
}

public class UpdateServiceRequestStatusRequest
{
    public required string Status { get; set; }
}

public class SubmitServiceReviewRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
}
