using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.ListOutbox;

/// <summary>One effect emission as its latest ledger row leaves it (engine spec §5.7, §6.3).</summary>
/// <param name="Sequence">The ledger sequence of that latest row.</param>
/// <param name="At">When that row was written.</param>
/// <param name="Identity">The stable effect identity.</param>
/// <param name="Command">The emission's command id.</param>
/// <param name="Kind">The effect block.</param>
/// <param name="Origin">'engine' or 'recipe'.</param>
/// <param name="State">Pending, dispatched or refused.</param>
/// <param name="Instance">The instance address.</param>
/// <param name="Code">The refusal code; null unless refused.</param>
public sealed record OutboxCommandView(
    long Sequence,
    DateTimeOffset At,
    string Identity,
    string Command,
    BlockId Kind,
    CommandOrigin Origin,
    EffectState State,
    InstanceAddress Instance,
    string? Code);
