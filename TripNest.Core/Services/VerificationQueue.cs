using System.Threading.Channels;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Unbounded in-memory <see cref="Channel{T}"/> backing <see cref="IVerificationQueue"/>.
/// Registered as a singleton so the API request path and the hosted processor share it.
/// </summary>
public class VerificationQueue : IVerificationQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public void Enqueue(string verificationId)
    {
        // Unbounded channel writes never block / never fail under normal operation.
        _channel.Writer.TryWrite(verificationId);
    }

    public ValueTask<string> DequeueAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAsync(cancellationToken);
}
