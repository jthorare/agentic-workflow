using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Input;

/// <summary>
/// The Step inputs of the PR babysitter's agent Steps (PB spec §10.1): 'input.pr.context' is descriptor-only until the forge port lands; 'input.phase-packet' and 'input.handoff-note' have their runtimes in <see cref="PhasePacketInput"/> and <see cref="HandoffNoteInput"/> (their descriptors live here, so the pinned hashes stay).
/// </summary>
internal static class PrInputs
{
    /// <summary>'input.pr.context': the PR's title, body, diff, failing check excerpts and the comment thread (fenced).</summary>
    public static BlockDescriptor Context { get; } = Of("input.pr.context");

    /// <summary>'input.phase-packet': the verified phase.json and design.md.</summary>
    public static BlockDescriptor PhasePacket { get; } = Of("input.phase-packet");

    /// <summary>'input.handoff-note' (W24): the latest 'fact:handoffNote'.</summary>
    public static BlockDescriptor HandoffNote { get; } = Of("input.handoff-note");

    private static BlockDescriptor Of(string id) => new(
        BlockId.From(id), BlockKind.StepInput, Version: 1, Inputs: [], Outputs: [], Outcomes: [], Capability.None, Parameters: [], Rung: null, DeskCapable: false);
}
