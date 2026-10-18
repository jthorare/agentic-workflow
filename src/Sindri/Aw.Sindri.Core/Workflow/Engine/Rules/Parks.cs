using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>Where an instance stands in its recipe whatever is suspended over it (spec §5.1), and how a park reason is spelled.</summary>
internal static class Parks
{
    /// <summary>The recipe state the instance is in: the frame's, whatever suspension is over it; null once cancelled.</summary>
    /// <param name="instance">The instance.</param>
    /// <returns>The recipe state, or null.</returns>
    public static string? RecipeState(WorkflowInstance instance) =>
        instance.Frame.State == EngineStates.Cancelled ? null : instance.Frame.State;

    /// <summary>A park reason as status and eval lines spell it.</summary>
    /// <param name="reason">The reason.</param>
    /// <returns>Its name.</returns>
    public static string Name(ParkReason reason) => reason switch
    {
        ParkReason.Rounds => "rounds",
        ParkReason.NoProgress => "no-progress",
        ParkReason.Budget => "budget",
        ParkReason.NeedsAnswer => "needs-answer",
        ParkReason.Timeout => "timeout",
        ParkReason.SpecConflict => "spec-conflict",
        ParkReason.Launch => "launch",
        ParkReason.NoRuntime or _ => "SND-WF-010",
    };
}
