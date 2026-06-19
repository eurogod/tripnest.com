namespace TripNest.Core.Interfaces.Services;

/// <summary>
/// In-process queue of verification request IDs awaiting background processing
/// (NIA lookup + face match). Decouples the HTTP request from the slow sidecar
/// calls so <c>StartVerification</c> can return immediately with a Pending status.
/// </summary>
public interface IVerificationQueue
{
    /// <summary>Queues a verification request ID for background processing.</summary>
    void Enqueue(string verificationId);

    /// <summary>Awaits the next queued verification request ID.</summary>
    ValueTask<string> DequeueAsync(CancellationToken cancellationToken);
}
