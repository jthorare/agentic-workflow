using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The design station's lens round and log Steps (line spec §5.2, Appendix C.1; LN-Y1): descriptors only. 'step.lens.run', the agent
/// Step between them, is in <see cref="LensSteps"/> (shared with the review loop).
/// </summary>
internal static class LensRoundSteps
{
    /// <summary>
    /// 'step.lens.round': queues the design lens pack's lenses that apply to the scope's surface kinds ('fact:lensQueue') and records the others as not applicable, with the reason, in 'fact:lensRounds'; 'ready'.
    /// The pack is the approved profile's (workflow.packs.lenses.design, a built-in pack); 'pack' names that profile key.
    /// </summary>
    public static BlockDescriptor Round { get; } = BlockStep("step.lens.round", Capability.Tools)
        .Takes(new ParameterSpec("pack", ParameterType.Text, Required: false, IsCapabilityField: false))
        .Reads("fact:scope", "fact:packs", "fact:lensRounds", "fact:changedSections").Reports("ready")
        .Writes(On("fact:lensQueue", "ready"), On("fact:lensRounds", "ready"));

    /// <summary>
    /// 'step.lens.log': appends the report to the lens rounds; 'next' (a lens is still queued), 'clean' or 'findings'. 'needsOwner' instead of 'findings' when a stopping finding carries
    /// a typed escalation (owner, 2026-10-19): the same findings, and every typed question of the round, all at once as the list 'fact:escalation' (owner, 2026-10-23). Nothing else reaches a human.
    /// </summary>
    public static BlockDescriptor Log { get; } = BlockStep("step.lens.log", Capability.None)
        .Reads("fact:lensReport", "fact:lensRounds", "fact:lensQueue").Reports("next", "clean", "findings", "needsOwner")
        .Writes(Every("fact:lensRounds"), On("fact:lensQueue", "next"), On("fact:findings", "findings", "needsOwner"), On("fact:escalation", "needsOwner"));
}
