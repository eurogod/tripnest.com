using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

/// <summary>
/// The single authority on which escrow status transitions are legal, and the only way code
/// should move an escrow between states. <see cref="Transition"/> validates the move, applies it,
/// and returns the <see cref="EscrowEvent"/> audit record — callers add that record and save it
/// in the same SaveChanges as the escrow, so the trail is atomically consistent with the row.
/// Callers may (and do) run their own friendlier guard messages first; this is the backstop.
/// </summary>
public static class EscrowStateMachine
{
    private static readonly IReadOnlyDictionary<EscrowStatus, EscrowStatus[]> Allowed =
        new Dictionary<EscrowStatus, EscrowStatus[]>
        {
            // Pending funds either get captured or the booking is cancelled before payment.
            [EscrowStatus.Pending] = new[] { EscrowStatus.HeldInEscrow, EscrowStatus.Refunded },
            [EscrowStatus.HeldInEscrow] = new[] { EscrowStatus.Released, EscrowStatus.Refunded, EscrowStatus.Disputed },
            [EscrowStatus.Disputed] = new[] { EscrowStatus.Released, EscrowStatus.Refunded },
            // Terminal: money has already moved.
            [EscrowStatus.Released] = Array.Empty<EscrowStatus>(),
            [EscrowStatus.Refunded] = Array.Empty<EscrowStatus>(),
        };

    public static bool CanTransition(EscrowStatus from, EscrowStatus to) =>
        Allowed.TryGetValue(from, out var allowed) && allowed.Contains(to);

    /// <summary>Applies a validated transition to the escrow and returns its audit event.</summary>
    /// <exception cref="InvalidOperationException">The transition is not legal.</exception>
    public static EscrowEvent Transition(Escrow escrow, EscrowStatus to, string actor, string? reason = null)
    {
        if (!CanTransition(escrow.Status, to))
            throw new InvalidOperationException($"Escrow cannot move from '{escrow.Status}' to '{to}'");

        var evt = new EscrowEvent
        {
            EscrowId = escrow.Id,
            BookingId = escrow.BookingId,
            FromStatus = escrow.Status,
            ToStatus = to,
            Actor = actor,
            Reason = reason
        };
        escrow.Status = to;
        return evt;
    }
}
