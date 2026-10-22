using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// The PR babysitter's and docs sync's effects (PB spec §10.1): descriptors only until the forge port gives them a runtime; a served
/// instance's dispatch of one is refused as an effect with no runtime. 'effect.pr.merge' and 'effect.pr.merge-docs' act with a
/// human's authority (W23, RCP-HUMAN-001).
/// </summary>
internal static class PrEffects
{
    /// <summary>'effect.pr.push': pushes the workspace head to the PR head with a lease; never rebases onto commits it didn't make.</summary>
    public static BlockDescriptor Push { get; } = Of("effect.pr.push", humanTrigger: false, "pr", "lease", "workspace", "cause");

    /// <summary>'effect.pr.reply': thread, reaction, fixed, fixedReplies, none or hold forms.</summary>
    public static BlockDescriptor Reply { get; } = Of("effect.pr.reply", humanTrigger: false, "form", "reason", "comment", "decision", "fix", "body", "pr", "push");

    /// <summary>'effect.pr.record': appends a recheck or docs row to the merge record.</summary>
    public static BlockDescriptor Record { get; } = Of("effect.pr.record", humanTrigger: false, "kind", "result");

    /// <summary>'effect.pr.rerun-checks': asks the forge to re-run the PR's checks.</summary>
    public static BlockDescriptor RerunChecks { get; } = Of("effect.pr.rerun-checks", humanTrigger: false, "pr", "at");

    /// <summary>'effect.pr.restack': restacks the PR onto its new base.</summary>
    public static BlockDescriptor Restack { get; } = Of("effect.pr.restack", humanTrigger: false, "pr") with
    {
        // The hash moves with this output (TODO: pin the new hash in PrRestackEffectContract once the lane has built it).
        Outputs = [new OutputSpec(FactKey.From("fact:restack"), [.. new[] { "restacked", "nothing", "conflict", "refused" }.Select(OutcomeName.From)])],
    };

    /// <summary>'effect.pr.create-stacked': opens the stacked docs PR on the source PR's head.</summary>
    public static BlockDescriptor CreateStacked { get; } = Of("effect.pr.create-stacked", humanTrigger: false, "pr", "on", "workspace", "title");

    /// <summary>'effect.pr.merge': merges with the forge's expected-head check; human trigger.</summary>
    public static BlockDescriptor Merge { get; } = Of("effect.pr.merge", humanTrigger: true, "pr", "at", "method");

    /// <summary>'effect.pr.merge-docs': retargets, checks and merges the stacked docs PR; human trigger.</summary>
    public static BlockDescriptor MergeDocs { get; } = Of("effect.pr.merge-docs", humanTrigger: true, "pr");

    private static BlockDescriptor Of(string id, bool humanTrigger, params string[] parameters) => new(
        BlockId.From(id), BlockKind.Effect, Version: 1, Inputs: [], Outputs: [], Outcomes: [], Capability.Effects,
        [.. parameters.Select(p => new ParameterSpec(p, ParameterType.Text, Required: false, IsCapabilityField: false))],
        Rung: null, DeskCapable: false, HumanTrigger: humanTrigger ? true : null);
}
