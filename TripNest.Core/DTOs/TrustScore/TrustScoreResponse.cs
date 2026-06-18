namespace TripNest.Core.DTOs.TrustScore;

public class TrustScoreResponse
{
    public required string SubjectId { get; set; }
    public required string SubjectType { get; set; }
    public decimal FinalScore { get; set; }
    public string Trend { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal VerificationComponent { get; set; }
    public decimal HistoryComponent { get; set; }
    public decimal FeedbackComponent { get; set; }
}
