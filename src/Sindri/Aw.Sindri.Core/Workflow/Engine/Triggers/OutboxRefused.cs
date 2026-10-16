using System.Globalization;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>
/// An effect's recheck refused it (spec §5.7): 'outbox.refused', written by the effect's activity before it ran. The
/// instance parks with spec-conflict of that kind, unless the same revoke already parked it.
/// </summary>
/// <param name="Actor">'system:dispatcher'.</param>
/// <param name="Effect">The refused effect as the dispatch emitted it: its kind, identity and arguments.</param>
/// <param name="Code">The refusal: SND-RECIPE-005 (revoked), SND-RECIPE-006 (drift), SND-WF-011 (not allowlisted) or SND-WF-010 (the setup the effect needs isn't there: no commit, a repo the profile lacks, no 'fact:item').</param>
/// <param name="Sequence">The 'outbox.refused' event's ledger sequence.</param>
/// <param name="Reason">The cause, for a setup failure (SND-WF-010); the park names it.</param>
public sealed record OutboxRefused(string Actor, RefusedEffect Effect, string Code, long Sequence, string? Reason = null) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => TriggerNames.OutboxRefused;

    /// <inheritdoc/>
    public override string KeyPart => string.Create(CultureInfo.InvariantCulture, $"outbox.refused:{Sequence}");
}
