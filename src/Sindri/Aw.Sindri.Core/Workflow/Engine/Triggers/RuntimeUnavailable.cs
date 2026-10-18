namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>The launch found no agent runtime on this box (SND-LAUNCH-014, -017; sandbox spec SB16, N1): a deskCapable Step runs at the desk in the same frame, with no round counted and no Step failure; any other Step parks 'SND-WF-010' with the sandbox marker.</summary>
/// <param name="Actor">The service that saw the refusal.</param>
/// <param name="Run">The agent run whose launch was refused; a trigger for another run is stale.</param>
/// <param name="Code">The launch code ('SND-LAUNCH-0…').</param>
/// <param name="Reason">The refusal's reason, or null.</param>
/// <param name="Source">The credential source for -017, otherwise null.</param>
/// <param name="Detail">The launcher's message, scrubbed.</param>
public sealed record RuntimeUnavailable(string Actor, RunToken Run, string Code, string? Reason, string? Source, string Detail) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => TriggerNames.RuntimeUnavailable;

    /// <inheritdoc/>
    public override string KeyPart => "runtime.unavailable:run:" + Run.Value;
}
