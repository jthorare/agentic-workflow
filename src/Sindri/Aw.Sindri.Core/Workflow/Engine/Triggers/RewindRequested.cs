namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>'workflow rewind' (#1270): re-enters an earlier state of the recipe and keeps every fact; a parked instance's park ends, a running desk or block run is replaced, and a live agent refuses it.</summary>
/// <param name="Actor">The caller (a launched agent session is refused).</param>
/// <param name="Stamp">The nonce and argument digest.</param>
/// <param name="To">The state to re-enter.</param>
/// <param name="EndedLaunch">The launch of the open run whose session the ledger shows ended (failed, idle or lost), set by the command service; a run whose launch is not this one is live as far as a rewind can tell.</param>
public sealed record RewindRequested(string Actor, CommandStamp Stamp, string To, string? EndedLaunch = null) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => TriggerNames.Rewind;

    /// <inheritdoc/>
    public override string KeyPart => Stamp.KeyPart(TriggerNames.Rewind);
}
