using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The ship station's Steps (line spec §7.5, C.5): descriptors only, so each runs on the desk until its runtime ships.
/// 'step.ship.open' stands in for the 'effect.pr.open' wait: a human opens or refuses the pull request, nothing reaches the forge.
/// </summary>
internal static class ShipSteps
{
    /// <summary>'step.ship.green': the local GREEN gate on the head; 'green' or 'red' (with 'fact:findings').</summary>
    public static BlockDescriptor Green { get; } = BlockStep("step.ship.green", Capability.Tools).Reads("fact:phasePacket", "fact:workspace").Reports("green", "red").Long(CommandStepLimits.GreenMaxDuration).Writes(On("fact:green", "green"), On("fact:findings", "red"));

    /// <summary>'step.ship.pack': assembles the hand-off draft, the evidence pack as the PR body.</summary>
    public static BlockDescriptor Pack { get; } = BlockStep("step.ship.pack", Capability.Tools).Reads("fact:phasePacket", "fact:proofPack", "fact:review", "fact:lineContext").Reports("packed").Writes(On("fact:handoffDraft", "packed"));

    /// <summary>'step.ship.freeze': proof, review and hand-off agree on one head; 'frozen' or 'stale' (with 'fact:findings').</summary>
    public static BlockDescriptor Freeze { get; } = BlockStep("step.ship.freeze", Capability.Tools).Reads("fact:handoffDraft", "fact:proofPack", "fact:review").Reports("frozen", "stale").Writes(On("fact:handoff", "frozen"), On("fact:findings", "stale"));

    /// <summary>'step.ship.open': the desk opens (or updates) the pull request from the frozen hand-off, or refuses; 'fact:pr' on opened and updated.</summary>
    public static BlockDescriptor Open { get; } = HumanDeskStep("step.ship.open", Capability.None).Reads("fact:handoff", "fact:workspace").Reports("opened", "updated", "refused").Writes(On("fact:pr", "opened", "updated"));

    /// <summary>'step.ship.return': writes why the phase goes back to build.</summary>
    public static BlockDescriptor Return { get; } = BlockStep("step.ship.return", Capability.Tools).Reads("fact:findings").Reports("noted").Writes(On("fact:returnNotes", "noted"));
}
