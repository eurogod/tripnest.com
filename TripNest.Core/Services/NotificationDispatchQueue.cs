using System.Threading.Channels;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Unbounded in-memory <see cref="Channel{T}"/> backing <see cref="INotificationDispatchQueue"/>.
/// Registered as a singleton so the request path and the hosted dispatcher share it. Mirrors
/// <see cref="VerificationQueue"/>.
/// </summary>
public class NotificationDispatchQueue : INotificationDispatchQueue
{
    private readonly Channel<NotificationDispatchJob> _channel = Channel.CreateUnbounded<NotificationDispatchJob>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public void Enqueue(NotificationDispatchJob job)
    {
        // Unbounded channel writes never block / never fail under normal operation.
        _channel.Writer.TryWrite(job);
    }

    public ValueTask<NotificationDispatchJob> DequeueAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAsync(cancellationToken);
}
