using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Features.LaneInbox;

/// <summary>The LaneInbox request: a lane's hook asks for the messages the meta sent it.</summary>
/// <param name="Identity">The lane, from its environment.</param>
public sealed record LaneInboxRequest(LaneIdentity Identity)
{
    /// <summary>The event of the hook making the call (AW_META_HOOK); null for a call typed by hand.</summary>
    public string? HookEvent { get; init; }
}
