using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The owner's decision Step (DT spec §6.1): descriptor only; any caller may answer it (owner q93, 2026-10-23).
/// </summary>
internal static class OwnerSteps
{
    /// <summary>'step.owner.decide': the owner picks 'retry' (with 'fact:notes') or 'stop'.</summary>
    public static BlockDescriptor Decide { get; } = DeskStep("step.owner.decide", Capability.None).Reports("retry", "stop").Writes(On("fact:notes", "retry"));
}
