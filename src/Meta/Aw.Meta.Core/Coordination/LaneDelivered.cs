using Aw.Meta.Core.Ids;

namespace Aw.Meta.Core.Coordination;

/// <summary>What one delivery to a non-Claude lane did.</summary>
/// <param name="Id">The stored message's id ("lane-send:&lt;lane&gt;:&lt;n&gt;").</param>
/// <param name="Woken">Whether the idle lane was woken with a typed line.</param>
/// <param name="WakeSkipped">Why it was not woken; null when woken.</param>
/// <param name="Attention">Set when the message was stored but the lane can't act on it (launch-only, or its hooks aren't running); null otherwise.</param>
/// <param name="ViewerAttached">Whether a tmux client was attached to the lane's session when it was woken.</param>
public sealed record LaneDelivered(MessageId Id, bool Woken, string? WakeSkipped, string? Attention, bool ViewerAttached);
