using TripNest.Core.Enums;

namespace TripNest.Core.Models;

public class TrustScoreSnapshot
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string SubjectType { get; set; }
    public required string SubjectId { get; set; }
    public decimal VerificationComponent { get; set; }
    public decimal HistoryComponent { get; set; }
    public decimal FeedbackComponent { get; set; }
    public decimal FinalScore { get; set; }
    public TrustScoreTrend Trend { get; set; } = TrustScoreTrend.Stable;
    public DateOnly SnapshotDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
