using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>
/// One ledger row of an effect (engine spec §5.6, §5.7, §6.3; engine P6): the payload of an <see cref="OutboxEvents"/>
/// event. It names its instance in 'instance', as every 'workflow.*' event does, so the bridge's causation route
/// ('ICommandOrigins') finds the instance a reply event's causation names.
/// </summary>
/// <param name="Identity">The stable effect identity ('{instance id}:{entry}:{index}'; an engine notice's ends in 'n{version}'; 'effect.claim.ref' keys by item through workflow_claims).</param>
/// <param name="Command">This emission's command id: a re-emission (a 'retry', a resumed dispatch park) gets a new one under the same identity.</param>
/// <param name="Kind">The effect block ('effect.notify.desk', 'effect.claim.ref').</param>
/// <param name="Origin">'engine' or 'recipe': the origin decides the exemption from the recheck, not the kind.</param>
/// <param name="State">Pending, dispatched, refused (the recheck), or cancelled (a cancel of the instance within the dispatch delay; no recheck ran).</param>
/// <param name="Instance">The instance address.</param>
/// <param name="Code">The refusal's code (SND-RECIPE-005, SND-RECIPE-006, SND-WF-011); null unless refused.</param>
/// <param name="Reason">The refusal's message, a cancelled row's 'cancelled: …', or a dispatched effect's note ('already sent'); null when there is none.</param>
public sealed record EffectRow(
    string Identity,
    string Command,
    BlockId Kind,
    CommandOrigin Origin,
    EffectState State,
    InstanceAddress Instance,
    string? Code = null,
    string? Reason = null);
