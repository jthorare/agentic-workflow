using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// The PR babysitter's and docs sync's effects (PB spec §10.1): their descriptors. Push, record, rerun-checks, merge, reply, purge-marks, create-stacked and merge-docs have runtimes (PrPushEffect, PrRecordEffect,
/// PrRerunChecksEffect, PrMergeEffect, PrReplyEffect, PrPurgeMarksEffect, PrCreateStackedEffect, PrMergeDocsEffect), and so does open (PrOpenEffect, its descriptor its own); and restack too (PrRestackEffect). 'effect.pr.merge' and 'effect.pr.merge-docs' act with a
/// human's authority (W23, RCP-HUMAN-001).
/// </summary>
internal static class PrEffects
{
    /// <summary>'effect.pr.push': pushes the workspace head to the PR head with a lease; never rebases onto commits it didn't make.</summary>
    public static BlockDescriptor Push { get; } = Of("effect.pr.push", humanTrigger: false, "pr", "lease", "workspace", "cause")
        with
    { Outputs = [new OutputSpec(FactKey.From("fact:push"), []),], };

    /// <summary>'effect.pr.reply': thread, reaction, fixed, fixedReplies, none or hold forms.</summary>
    public static BlockDescriptor Reply { get; } = Of("effect.pr.reply", humanTrigger: false, "form", "reason", "comment", "decision", "fix", "body", "pr", "push");

    /// <summary>'effect.pr.record': appends a recheck, docs, docsMerge or end row to the merge record.</summary>
    public static BlockDescriptor Record { get; } = Of("effect.pr.record", humanTrigger: false, "kind", "result", "terminal", "notes", "outcome", "trigger");

    /// <summary>'effect.pr.rerun-checks': asks the forge to re-run the PR's checks.</summary>
    public static BlockDescriptor RerunChecks { get; } = Of("effect.pr.rerun-checks", humanTrigger: false, "pr", "at");

    /// <summary>'effect.pr.restack': rebases the PR onto its new base without the old base's commits, scans, retargets and pushes with a lease.</summary>
    public static BlockDescriptor Restack { get; } = Of("effect.pr.restack", humanTrigger: false, "pr", "workspace", "from")
        with
    { Outputs = [new OutputSpec(FactKey.From("fact:restack"), [OutcomeName.From("restacked"), OutcomeName.From("nothing"), OutcomeName.From("conflict"), OutcomeName.From("refused")]),], };

    /// <summary>'effect.pr.create-stacked': opens (or updates) the stacked docs PR on the source PR's head branch.</summary>
    public static BlockDescriptor CreateStacked { get; } = Of("effect.pr.create-stacked", humanTrigger: false, "pr", "on", "workspace", "title")
        with
    { Outputs = [new OutputSpec(FactKey.From("fact:docsPr"), [OutcomeName.From("opened"), OutcomeName.From("updated")]),], };

    /// <summary>'effect.pr.merge': merges with the forge's expected-head check; human trigger.</summary>
    public static BlockDescriptor Merge { get; } = Of("effect.pr.merge", humanTrigger: true, "pr", "at", "method", "actor");

    /// <summary>'effect.pr.purge-marks' (W26, IDEA-033): a babysitter's terminal cleanup, deleting the PR's comment marks.</summary>
    public static BlockDescriptor PurgeMarks { get; } = Of("effect.pr.purge-marks", humanTrigger: false, "pr");

    /// <summary>'effect.pr.merge-docs': retargets and merges the stacked docs PR once its checks pass; human trigger.</summary>
    public static BlockDescriptor MergeDocs { get; } = Of("effect.pr.merge-docs", humanTrigger: true, "pr", "method", "trigger", "actor")
        with
    { Outputs = [new OutputSpec(FactKey.From("fact:docsMerge"), [OutcomeName.From("merged"), OutcomeName.From("pending"), OutcomeName.From("none")]),], };

    private static BlockDescriptor Of(string id, bool humanTrigger, params string[] parameters) => new(
        BlockId.From(id), BlockKind.Effect, Version: 1, Inputs: [], Outputs: [], Outcomes: [], Capability.Effects,
        [.. parameters.Select(p => new ParameterSpec(p, ParameterType.Text, Required: false, IsCapabilityField: false))],
        Rung: null, DeskCapable: false, HumanTrigger: humanTrigger ? true : null);
}
