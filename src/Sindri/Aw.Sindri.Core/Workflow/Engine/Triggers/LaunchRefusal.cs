using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>What the two launch-refusal triggers carry (sandbox spec §6): the refused run and the launcher's code, reason, source and message.</summary>
/// <param name="Actor">The service that saw the refusal.</param>
/// <param name="Run">The agent run whose launch was refused; a trigger for another run is stale.</param>
/// <param name="Code">The launch code ('SND-LAUNCH-0…').</param>
/// <param name="Reason">The refusal's reason, or null.</param>
/// <param name="Source">The credential source for -017, otherwise null.</param>
/// <param name="Detail">The launcher's message, scrubbed.</param>
public abstract record LaunchRefusal(string Actor, RunToken Run, string Code, string? Reason, string? Source, string Detail) : Trigger(Actor)
{
    /// <summary>The marker a park caused by this refusal carries.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public SandboxPark Marker => new(Code, Reason, Source);
}
