using Aw.Meta.Core.Ids;

namespace Aw.Meta.Features.LaneRecycle;

// What a recycle will act on, once every refusal has been checked. SwitchTo is the agent routing.lanes assigns when it is not the one the lane runs.
internal sealed record RecyclePlan(string Team, string Lane, SessionId OldSession, long? OldTokens, string Digest, string? SwitchTo = null, string? SwitchModel = null)
{
    internal string Session => Team + "-" + Lane;

    internal string ContinueLine => "Read and continue from " + Digest;
}
