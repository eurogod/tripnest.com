namespace TripNest.Core.DTOs.Caretakers;

public class AssignCaretakerRequest
{
    public required string PropertyId { get; set; }
    public required string CaretakerId { get; set; }
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
