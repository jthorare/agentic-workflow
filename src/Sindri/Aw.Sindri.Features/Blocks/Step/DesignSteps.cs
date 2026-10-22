using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The design station's Steps (line spec §4, Appendix C.1; LN-Y1): descriptors only, desk-capable, so a served instance parks at
/// one with SND-WF-010 until the agent and block runtimes run them. 'bundle' names a pack (the style bundle, or the lens bundle)
/// and is descriptor-only until LN-PK resolves it. Fact and outcome names are camelCase.
/// </summary>
internal static class DesignSteps
{
    private static readonly string[] _fromScope = ["fresh", "revise"];

    /// <summary>'step.design.intake': loads the frozen scope packet and the style and lens packs; 'fresh' or 'revise' (a revision from stop 2, which also loads the previous design packet into 'fact:design'); every outcome carries the eight round-1 seeds.</summary>
    public static BlockDescriptor Intake { get; } = BlockStep("step.design.intake", Capability.None)
        .Reads("fact:scopePacket", "fact:designRevision", "fact:priorDesignPacket").Reports(_fromScope)
        .Writes(Seeds());

    /// <summary>'step.design.draft': one spec draft in the house style; 'drafted'. After a revision it also reads the owner's picks ('designRevision', 'answers'), so a redraft keeps them.</summary>
    // TODO(W9/L-17): read-only capability (no descriptor field yet; the launch enforces it)
    public static BlockDescriptor Draft { get; } = AgentStep("step.design.draft", Capability.Tools)
        .Reads("fact:scope", "fact:packs", "fact:design", "fact:findings", "fact:changedSections", "fact:designRevision", "fact:answers").Reports("drafted")
        .Takes(Bundle())
        .Writes(On("fact:design", "drafted"));

    /// <summary>'step.design.apply': folds the stop-2 revision into the sections it affects; 'applied' with the changed sections.</summary>
    // TODO(W9/L-17): read-only capability (no descriptor field yet; the launch enforces it)
    public static BlockDescriptor Apply { get; } = AgentStep("step.design.apply", Capability.Tools)
        .Reads("fact:scope", "fact:packs", "fact:design", "fact:designRevision", "fact:answers").Reports("applied")
        .Takes(Bundle())
        .Writes(On("fact:design", "applied"), On("fact:changedSections", "applied"));

    /// <summary>'step.design.check': the draft-stage design checks (coverage, signals, decisions, contract edits, style); 'pass' or 'gaps' with findings.</summary>
    public static BlockDescriptor Check { get; } = BlockStep("step.design.check", Capability.Tools)
        .Reads("fact:scope", "fact:packs", "fact:design").Reports("pass", "gaps").Writes(On("fact:findings", "gaps"));

    /// <summary>'step.design.revise': fixes the lens findings and answers the rest in writing; 'revised' with the changed sections.</summary>
    // TODO(W9/L-17): read-only capability (no descriptor field yet; the launch enforces it)
    public static BlockDescriptor Revise { get; } = AgentStep("step.design.revise", Capability.Tools)
        .Reads("fact:scope", "fact:packs", "fact:design", "fact:findings", "fact:answers").Reports("revised")
        .Takes(Bundle())
        .Writes(On("fact:design", "revised"), On("fact:changedSections", "revised"));

    /// <summary>'step.design.lens-desk': the lens rounds hit their cap with findings left; human-only: accept them as known risks, revise with notes, or drop.</summary>
    public static BlockDescriptor LensDesk { get; } = HumanDeskStep("step.design.lens-desk", Capability.None)
        .Reads("fact:findings", "fact:lensRounds").Reports("accept", "revise", "drop")
        .Writes(On("fact:accepted", "accept"), On("fact:answers", "revise"));

    /// <summary>'step.design.evidence': checks the design evidence and assembles the candidate design packet; 'complete' or 'incomplete' with findings.</summary>
    public static BlockDescriptor Evidence { get; } = BlockStep("step.design.evidence", Capability.Tools)
        .Reads("fact:scope", "fact:packs", "fact:design", "fact:findings", "fact:lensRounds", "fact:accepted")
        .Reports("complete", "incomplete")
        .Writes(On("fact:designPacket", "complete"), On("fact:design", "complete"), On("fact:lensRounds", "complete"), On("fact:findings", "incomplete"));

    private static ParameterSpec Bundle() => new("bundle", ParameterType.Text, Required: false, IsCapabilityField: false);

    private static OutputSpec[] Seeds() =>
    [
        On("fact:scope", _fromScope), On("fact:packs", _fromScope), On("fact:design", _fromScope), On("fact:findings", _fromScope),
        On("fact:lensRounds", _fromScope), On("fact:changedSections", _fromScope), On("fact:answers", _fromScope), On("fact:accepted", _fromScope),
    ];
}
