using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Features.LaneStopCheck;

/// <summary>The LaneStopCheck request: a lane's stop hook asks whether the lane owes the meta a reply.</summary>
/// <param name="Identity">The lane, from its environment.</param>
/// <param name="HookActive">Whether the stop hook is already continuing a block.</param>
/// <param name="MaxBlocks">How many identical blocks in a row the lane gets (AW_META_STOP_MAX).</param>
public sealed record LaneStopCheckRequest(LaneIdentity Identity, bool HookActive, int MaxBlocks)
{
    /// <summary>The event of the hook making the call (AW_META_HOOK); null for a call typed by hand.</summary>
    public string? HookEvent { get; init; }
}
