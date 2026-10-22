using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The PR babysitter's Steps (PB spec §10.1, C.1): descriptors only, so the recipe validates, evals and runs on the desk until each
/// block has a runtime. The 'block' Steps need the forge port, the 'agent' ones a session, and the 'desk' ones a human.
/// </summary>
internal static class PrSteps
{
    private static readonly string[] _seeds = ["fact:workspace", "fact:decision", "fact:ownerNote", "fact:handoffNote", "fact:fix", "fact:reply", "fact:recheck"];

    /// <summary>'step.pr.intake': verifies the hand-off and seeds the babysitter (an empty hand-off for a hand start); 'seeded'.</summary>
    public static BlockDescriptor Intake { get; } = BlockStep("step.pr.intake", Capability.Network).Takes(Text("pr"), Text("handoff"), Text("phase-packet"), Text("line-context"), Text("workspace"))
        .Reports("seeded").Writes([.. _seeds.Select(f => On(f, "seeded"))]);

    /// <summary>'step.pr.snapshot': reads the PR's state in §4.3's priority order; the outcome names what to do next.</summary>
    public static BlockDescriptor Snapshot { get; } = BlockStep("step.pr.snapshot", Capability.Network).Takes(Text("pr"), Text("handoff"), Text("recheck"), Text("workspace"))
        .Reports("mergedDocsOpen", "merged", "closed", "restack", "conflict", "ciRed", "comments", "stale", "docsRed", "pending", "green")
        .Writes(Every("fact:prState"), On("fact:mergedSha", "merged", "mergedDocsOpen"));

    /// <summary>'step.pr.ready-check': whether the PR changed while it waits to be merged; 'same' or 'changed'.</summary>
    public static BlockDescriptor ReadyCheck { get; } = BlockStep("step.pr.ready-check", Capability.Network).Takes(Text("pr"), Text("at")).Reports("same", "changed").Writes();

    /// <summary>'step.pr.next-comment': takes the oldest unhandled thread; authorship is checked before content.</summary>
    public static BlockDescriptor NextComment { get; } = BlockStep("step.pr.next-comment", Capability.Network).Takes(Text("pr"), Text("bots")).Reads("fact:prState")
        .Reports("untrusted", "bot", "comment", "drainedUnpushed", "drained").Writes(On("fact:comment", "untrusted", "bot", "comment"));

    /// <summary>'step.pr.held-comment': takes the oldest held thread for the owner's walk.</summary>
    public static BlockDescriptor HeldComment { get; } = BlockStep("step.pr.held-comment", Capability.Network).Takes(Text("pr")).Reads("fact:prState")
        .Reports("comment", "drainedUnpushed", "drained").Writes(On("fact:comment", "comment"));

    /// <summary>'step.pr.ci-fix': an agent fixes the failing checks in the workspace; Sindri pushes.</summary>
    public static BlockDescriptor CiFix { get; } = AgentStep("step.pr.ci-fix", Capability.Tools | Capability.WriteScope).Reads("fact:prState")
        .Reports("fixed", "flaky", "cannotFix").Writes(Every("fact:handoffNote"));

    /// <summary>'step.pr.fix-comment': an agent makes the change a comment asks for (effort 'full', 'cheap' or 'bot'), or declines it.</summary>
    public static BlockDescriptor FixComment { get; } = AgentStep("step.pr.fix-comment", Capability.Tools | Capability.WriteScope).Takes(Text("effort", required: true)).Reads("fact:comment")
        .Reports("fixed", "declined", "cannotFix").Writes(Every("fact:fix"), Every("fact:handoffNote"));

    /// <summary>'step.pr.reply-draft': an agent drafts the reply to a comment; Sindri checks and posts it.</summary>
    public static BlockDescriptor ReplyDraft { get; } = AgentStep("step.pr.reply-draft", Capability.Tools).Reads("fact:comment")
        .Reports("drafted", "noReply", "needsOwner").Writes(On("fact:reply", "drafted", "needsOwner"));

    /// <summary>'step.pr.owner-comment': the owner decides a held comment: 'fix' (with a note), 'reply' (with the text), 'skip' or 'later'.</summary>
    public static BlockDescriptor OwnerComment { get; } = HumanDeskStep("step.pr.owner-comment", Capability.None).Reads("fact:comment")
        .Reports("fix", "reply", "skip", "later").Writes(On("fact:ownerNote", "fix"), On("fact:reply", "reply"));

    /// <summary>'step.pr.rework-notes': the owner writes what the phase must change ('noted') or sends the PR back to the babysitter ('back').</summary>
    public static BlockDescriptor ReworkNotes { get; } = HumanDeskStep("step.pr.rework-notes", Capability.None).Reports("noted", "back").Writes(On("fact:reworkNotes", "noted"));

    /// <summary>'step.pr.docs-decide': the owner decides what to do when docs sync did not finish: 'retry', 'check', 'skip' or 'stop'.</summary>
    public static BlockDescriptor DocsDecide { get; } = HumanDeskStep("step.pr.docs-decide", Capability.None).Reports("retry", "check", "skip", "stop").Writes(On("fact:docsResult", "skip"));

    /// <summary>'step.pr.docs-merge-decide': the owner decides the open docs PR of a merged PR: 'merge', 'done' or 'leave'.</summary>
    public static BlockDescriptor DocsMergeDecide { get; } = HumanDeskStep("step.pr.docs-merge-decide", Capability.None).Reports("merge", "done", "leave").Writes();
}
