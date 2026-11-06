using Aw.Meta.Core.Ids;

namespace Aw.Meta.Features.LaneSend;

/// <summary>The LaneSend response.</summary>
/// <param name="Lane">The lane.</param>
/// <param name="Id">The sent message's id ("lane-send:&lt;lane&gt;:&lt;n&gt;").</param>
/// <param name="Woken">Whether the idle lane was woken with a typed line.</param>
/// <param name="WakeSkipped">Why it was not woken, when it was not; null when woken.</param>
/// <param name="Attention">Set when the message was stored but the lane can't act on it (launch-only, or its hooks aren't running); null otherwise.</param>
public sealed record LaneSendResponse(string Lane, MessageId Id, bool Woken, string? WakeSkipped, string? Attention)
{
    /// <summary>Whether a tmux client was attached to the lane's session when it was woken.</summary>
    public bool ViewerAttached { get; init; }
}
