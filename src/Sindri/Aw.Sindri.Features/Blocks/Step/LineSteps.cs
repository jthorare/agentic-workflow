using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The line's dispatch Steps (line spec §3.5, §8.2, LN-WF1): 'seed' and 'dispatch' are block Steps over the phase board, 'decide' is the owner's desk, human-only.
/// </summary>
internal static class LineSteps
{
    /// <summary>'step.line.seed': writes the board of the frozen plan, one waiting row per phase, and the empty event and edit facts.</summary>
    public static BlockDescriptor Seed { get; } = BlockStep("step.line.seed", Capability.None)
        .Reads("fact:phasePackets")
        .Reports("seeded")
        .Writes(On("fact:phaseBoard", "seeded"), On("fact:phaseEvent", "seeded"), On("fact:boardEdit", "seeded"));

    /// <summary>'step.line.dispatch': folds the last phase event and the owner's edit into the board; 'start' (with fact:nextPhase), 'full', 'done' or 'blocked'.</summary>
    public static BlockDescriptor Dispatch { get; } = BlockStep("step.line.dispatch", Capability.None)
        .Reads("fact:phaseBoard", "fact:phaseEvent", "fact:boardEdit")
        .Reports("start", "full", "done", "blocked")
        .Writes(Every("fact:phaseBoard"), Every("fact:phaseEvent"), Every("fact:boardEdit"), On("fact:nextPhase", "start"));

    /// <summary>'step.line.decide': the owner retries a failed phase, skips it and its dependents, or stops the line.</summary>
    public static BlockDescriptor Decide { get; } = HumanDeskStep("step.line.decide", Capability.None)
        .Reads("fact:phaseBoard")
        .Reports("retry", "skip", "stop")
        .Writes(On("fact:boardEdit", "retry", "skip"));
}
