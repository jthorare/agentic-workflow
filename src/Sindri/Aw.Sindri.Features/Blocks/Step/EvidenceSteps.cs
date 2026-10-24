using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The evidence blocks' Steps (DT spec §5; line spec DTC6: they read 'fact:phasePacket' and 'fact:built'): descriptors only. Each artifact an evidence run reports records its 'commit' and 'at' (DTC9: an Aw.Evidence change, not this one).
/// </summary>
internal static class EvidenceSteps
{
    /// <summary>'step.evidence.ui-plan': the UI evidence plan; 'planned' with 'fact:evidencePlan', or 'noTarget' with 'fact:evidenceSummary'.</summary>
    public static BlockDescriptor UiPlan { get; } = Plan("step.evidence.ui-plan");

    /// <summary>'step.evidence.api-plan': as 'step.evidence.ui-plan', for the API contract.</summary>
    public static BlockDescriptor ApiPlan { get; } = Plan("step.evidence.api-plan");

    /// <summary>'step.evidence.db-plan': as 'step.evidence.ui-plan', for the database.</summary>
    public static BlockDescriptor DbPlan { get; } = Plan("step.evidence.db-plan");

    /// <summary>'step.evidence.ui-run': the long browser run (DT spec §8.2); 'passed', 'failed' or 'broken', 'fact:evidenceSummary' on every one.</summary>
    public static BlockDescriptor UiRun { get; } = AgentStep("step.evidence.ui-run", Capability.Tools).Reads("fact:evidencePlan", "fact:built").Reports("passed", "failed", "broken").Writes(Every("fact:evidenceSummary"));

    /// <summary>'step.evidence.api-run': 'passed' or 'failed', 'fact:evidenceSummary' on both.</summary>
    public static BlockDescriptor ApiRun { get; } = Run("step.evidence.api-run");

    /// <summary>'step.evidence.db-run': 'passed' or 'failed', 'fact:evidenceSummary' on both.</summary>
    public static BlockDescriptor DbRun { get; } = Run("step.evidence.db-run");

    /// <summary>'step.evidence.none': records that nothing needs evidence; 'done' with 'fact:evidenceSummary'.</summary>
    public static BlockDescriptor None { get; } = BlockStep("step.evidence.none", Capability.None).Reads("fact:built").Reports("done").Writes(On("fact:evidenceSummary", "done"));

    /// <summary>'step.evidence.combine': the three summaries into one; 'done' with 'fact:evidenceSummary'.</summary>
    public static BlockDescriptor Combine { get; } = BlockStep("step.evidence.combine", Capability.None).Reads("fact:evidenceUi", "fact:evidenceApi", "fact:evidenceDb").Reports("done").Writes(On("fact:evidenceSummary", "done"));

    /// <summary>'step.evidence.desk': a human records evidence for a change no provider covers; human-only; 'recorded' or 'noneFound', summary and reason on both.</summary>
    public static BlockDescriptor Desk { get; } = DeskStep("step.evidence.desk", Capability.None).Reads("fact:built").Reports("recorded", "noneFound").Writes(Every("fact:evidenceSummary"), Every("fact:evidenceReason"));

    private static BlockDescriptor Plan(string id) => AgentStep(id, Capability.Tools).Reads("fact:phasePacket", "fact:built").Reports("planned", "noTarget").Writes(On("fact:evidencePlan", "planned"), On("fact:evidenceSummary", "noTarget"));

    private static BlockDescriptor Run(string id) => AgentStep(id, Capability.Tools).Reads("fact:evidencePlan", "fact:built").Reports("passed", "failed").Writes(Every("fact:evidenceSummary"));
}
