using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The bugfix blocks' Steps (DT spec §4, §6): descriptors only. Agent Steps run in a launched session (P8); the block Steps are the deterministic ones DT-J runs; 'step.intake.ticket' is the desk's. Outcome and fact names are camelCase (line spec DTC8).
/// </summary>
internal static class BugfixSteps
{
    /// <summary>'step.intake.ticket': the ticket read into facts; outcome 'done'.</summary>
    public static BlockDescriptor IntakeTicket { get; } = DeskStep("step.intake.ticket", Capability.None).Reports("done").Writes(On("fact:issue", "done"), On("fact:expected", "done"), On("fact:actual", "done"));

    /// <summary>'step.rootcause.investigate': 'diagnosed' with 'fact:rootCause', or 'unfixed' with 'fact:findings'.</summary>
    public static BlockDescriptor RootcauseInvestigate { get; } = AgentStep("step.rootcause.investigate", Capability.Tools).Reads("fact:issue", "fact:expected", "fact:actual").Reports("diagnosed", "unfixed").Writes(On("fact:rootCause", "diagnosed"), On("fact:findings", "unfixed"));

    /// <summary>'step.reproduce.ui-check': 'reproduced' with 'fact:check' and 'fact:baseline', or 'notReproduced' with 'fact:findings'.</summary>
    public static BlockDescriptor ReproduceUiCheck { get; } = AgentStep("step.reproduce.ui-check", Capability.Tools).Reads("fact:issue", "fact:rootCause").Reports("reproduced", "notReproduced").Writes(On("fact:check", "reproduced"), On("fact:baseline", "reproduced"), On("fact:findings", "notReproduced"));

    /// <summary>'step.reproduce.test-check': as 'step.reproduce.ui-check', with a test as the check.</summary>
    public static BlockDescriptor ReproduceTestCheck { get; } = AgentStep("step.reproduce.test-check", Capability.Tools).Reads("fact:issue", "fact:rootCause").Reports("reproduced", "notReproduced").Writes(On("fact:check", "reproduced"), On("fact:baseline", "reproduced"), On("fact:findings", "notReproduced"));

    /// <summary>'step.implement.fix': fixes the bug and prevents its class, tests first, in atomic commits; 'done' with 'fact:output' (the diff's description, ending with the filled handoff block).</summary>
    public static BlockDescriptor ImplementFix { get; } = AgentStep("step.implement.fix", Capability.Tools | Capability.WriteScope).Reads("fact:issue", "fact:rootCause").Reports("done").Writes(On("fact:output", "done"));

    /// <summary>'step.bugfix.start-attempt': 'single' or 'split' with 'fact:attempt', or 'capReached'.</summary>
    public static BlockDescriptor StartAttempt { get; } = BlockStep("step.bugfix.start-attempt", Capability.Tools).Reads("fact:decision").Reports("single", "split", "capReached").Writes(On("fact:attempt", "single", "split"));

    /// <summary>'step.bugfix.record-candidate': 'recorded' with 'fact:candidate'.</summary>
    public static BlockDescriptor RecordCandidate { get; } = BlockStep("step.bugfix.record-candidate", Capability.Tools).Reads("fact:diff").Reports("recorded").Writes(On("fact:candidate", "recorded"));

    /// <summary>'step.bugfix.recheck': runs the reproduction check against the candidate; 'passed' or 'failed', 'fact:recheck' on both.</summary>
    public static BlockDescriptor Recheck { get; } = BlockStep("step.bugfix.recheck", Capability.Tools).Reads("fact:check", "fact:candidate").Reports("passed", "failed").Writes(Every("fact:recheck"));

    /// <summary>'step.bugfix.judge': the resolution check; 'resolved', 'partial', 'unresolved' or 'needsHuman', 'fact:verdict' on every one.</summary>
    public static BlockDescriptor Judge { get; } = BlockStep("step.bugfix.judge", Capability.Tools).Reads("fact:check", "fact:candidate", "fact:evidenceSummary").Reports("resolved", "partial", "unresolved", "needsHuman").Writes(Every("fact:verdict"));

    /// <summary>'step.report.write': the write-up; 'done' with 'fact:resolution'.</summary>
    public static BlockDescriptor ReportWrite { get; } = AgentStep("step.report.write", Capability.Tools).Reads("fact:issue", "fact:rootCause").Reports("done").Writes(On("fact:resolution", "done"));
}
