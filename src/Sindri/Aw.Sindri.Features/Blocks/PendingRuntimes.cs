using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks;

/// <summary>
/// The P7 block (spec §12), shipped in P1 as a descriptor with an unavailable runtime so P1 can validate both recipes.
/// At run time a gate without a runtime refuses (W5). The P6 effects have their runtimes (Blocks/Effect).
/// </summary>
internal static class PendingRuntimes
{
    /// <summary>'gate.route.startable': a stub over the profile's 'workflow.route' (P7).</summary>
    public static BlockDescriptor RouteStartable { get; } = new(
        BlockId.From("gate.route.startable"), BlockKind.Gate, Version: 1, Inputs: [], Outputs: [], Outcomes: [],
        Capability.None, Parameters: [], Rung.Deterministic, DeskCapable: false);
}
