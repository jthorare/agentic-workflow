using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The docs-sync station's Steps (PB spec §7, C.2): descriptors only, so each runs on the desk until its runtime ships. The stacked docs PR
/// is opened by 'effect.pr.create-stacked' and awaited as 'pr.stacked'.
/// </summary>
internal static class DocsSteps
{
    /// <summary>'step.docs.scope': lists the docs the pull request might leave stale; 'candidates', 'none', 'disabled' or 'notOpen'.</summary>
    public static BlockDescriptor Scope { get; } = BlockStep("step.docs.scope", Capability.Tools).Takes(Text("pr"), Text("phase-packet"), Text("paths"), Text("enabled")).Reads("fact:pr", "fact:phasePacket").Reports("candidates", "none", "disabled", "notOpen")
        .Writes(Every("fact:docsResult"), On("fact:docsScope", "candidates"), On("fact:prState", "candidates"), On("fact:findings", "candidates"), On("fact:handoffNote", "candidates"), On("fact:workspace", "candidates"));

    /// <summary>'step.docs.update': edits the listed docs in the docs worktree; 'changed', 'noChange' or 'blocked'.</summary>
    public static BlockDescriptor Update { get; } = AgentStep("step.docs.update", Capability.Tools | Capability.WriteScope).Reads("fact:docsScope", "fact:prState", "fact:findings", "fact:handoffNote", "fact:phasePacket", "fact:workspace").Reports("changed", "noChange", "blocked").Writes(Every("fact:handoffNote"));

    /// <summary>'step.docs.check': doc paths only, links resolve, the markdown parses; 'pass' or 'fail' (with 'fact:findings').</summary>
    public static BlockDescriptor Check { get; } = BlockStep("step.docs.check", Capability.Tools).Takes(Text("paths")).Reads("fact:docsScope", "fact:workspace").Reports("pass", "fail").Writes(On("fact:findings", "fail"));
}
