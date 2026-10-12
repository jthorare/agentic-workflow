using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks;

/// <summary>
/// The P6 and P7 blocks (spec §12), shipped in P1 as descriptors with unavailable runtimes so P1 can validate both
/// recipes. At run time a gate without a runtime refuses and an effect can't be dispatched (W5).
/// </summary>
internal static class PendingRuntimes
{
    /// <summary>'gate.route.startable': a stub over the profile's 'workflow.route' (P7).</summary>
    public static BlockDescriptor RouteStartable { get; } = new(
        BlockId.From("gate.route.startable"), BlockKind.Gate, Version: 1, Inputs: [], Outputs: [], Outcomes: [],
        Capability.None, Parameters: [], Rung.Deterministic, DeskCapable: false);

    /// <summary>
    /// 'effect.claim.ref' (P6): creates the marker ref 'sindri/{user}/{item}' in the item's repo, so it writes in a
    /// repo. It reads 'fact:item' for the repo and the item id.
    /// </summary>
    public static BlockDescriptor ClaimRef { get; } = new(
        BlockId.From("effect.claim.ref"), BlockKind.Effect, Version: 1, Inputs: [FactKey.From("fact:item")], Outputs: [], Outcomes: [],
        Capability.Effects | Capability.WriteScope, Parameters: [], Rung: null, DeskCapable: false);

    /// <summary>'effect.notify.desk' (P6): a needs-you notice on the human's desk.</summary>
    public static BlockDescriptor NotifyDesk { get; } = new(
        BlockId.From("effect.notify.desk"), BlockKind.Effect, Version: 1, Inputs: [], Outputs: [], Outcomes: [],
        Capability.Effects, Parameters: [], Rung: null, DeskCapable: false);
}
