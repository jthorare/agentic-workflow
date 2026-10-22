using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The prove station's own Steps (line spec §7.3, Appendix C.4): descriptors only, desk-capable, so each parks with SND-WF-010 at the
/// desk until its runtime ships. The hand test, verify and evidence run as children. 'step.prove.holdout' is an agent Step (the evaluator
/// model, LN-WF4). Fact and outcome names are camelCase.
/// </summary>
internal static class ProveSteps
{
    /// <summary>'step.prove.intake': loads the phase's proof signals and seeds the facts the later waits read; 'ready'.</summary>
    public static BlockDescriptor Intake { get; } = BlockStep("step.prove.intake", Capability.Tools)
        .Reads("fact:phasePacket", "fact:built", "fact:workspace", "fact:redTests").Reports("ready")
        .Writes(On("fact:findings", "ready"), On("fact:proof", "ready"), On("fact:holdoutResult", "ready"), On("fact:beforeAfter", "ready"));

    /// <summary>'step.prove.collect': hashes the files the hand test, verify and evidence children name and appends them to fact:proof as artifacts on the head; 'collected'.</summary>
    public static BlockDescriptor Collect { get; } = BlockStep("step.prove.collect", Capability.Tools)
        .Reads("fact:proof", "fact:built", "fact:workspace", "fact:manualTest", "fact:evidencePack", "fact:evidenceSummary").Reports("collected")
        .Writes(On("fact:proof", "collected"));

    /// <summary>'step.prove.before-after': each fix item's recorded script must fail at the base and pass at the head; 'pass' or 'fail', the result on both.</summary>
    public static BlockDescriptor BeforeAfter { get; } = BlockStep("step.prove.before-after", Capability.Tools)
        .Reads("fact:phasePacket", "fact:built", "fact:workspace").Reports("pass", "fail").Writes(Every("fact:beforeAfter"));

    /// <summary>'step.prove.holdout': runs the hidden scenarios against the head in the running app; 'ran' with the verdicts.</summary>
    public static BlockDescriptor Holdout { get; } = AgentStep("step.prove.holdout", Capability.Tools | Capability.WriteScope)
        .Reads("fact:built", "fact:workspace").Reports("ran").Writes(On("fact:holdoutResult", "ran"));

    /// <summary>'step.prove.holdout-log': records the verdicts, marks failures spent and checks the canary; 'passed', 'failed' or 'leaked'.</summary>
    public static BlockDescriptor HoldoutLog { get; } = BlockStep("step.prove.holdout-log", Capability.Tools)
        .Reads("fact:holdoutResult", "fact:built", "fact:phasePacket").Reports("passed", "failed", "leaked")
        .Writes(On("fact:holdoutResult", "passed", "failed"), On("fact:findings", "failed", "leaked"));

    /// <summary>'step.prove.evidence': the proof checks, then assembles and machine-freezes the proof pack; 'complete' or 'incomplete' with findings.</summary>
    public static BlockDescriptor Evidence { get; } = BlockStep("step.prove.evidence", Capability.Tools)
        .Reads("fact:phasePacket", "fact:built", "fact:proof", "fact:redTests", "fact:beforeAfter", "fact:holdoutResult")
        .Reports("complete", "incomplete").Writes(On("fact:proofPack", "complete"), On("fact:findings", "incomplete"));

    /// <summary>'step.prove.fail-log': every failure path ends here; writes what failed as findings for the builder; 'logged'.</summary>
    public static BlockDescriptor FailLog { get; } = BlockStep("step.prove.fail-log", Capability.None)
        .Reads("fact:findings", "fact:beforeAfter", "fact:holdoutResult").Reports("logged")
        .Writes(On("fact:findings", "logged"));
}
