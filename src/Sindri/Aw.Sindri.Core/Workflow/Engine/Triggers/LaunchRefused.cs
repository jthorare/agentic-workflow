namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>The launch was refused so that the instance parks as 'launch' (SND-LAUNCH-015, -016; sandbox spec SB16): the park's Resume starts a new run.</summary>
/// <param name="Actor">The service that saw the refusal.</param>
/// <param name="Run">The agent run whose launch was refused; a trigger for another run is stale.</param>
/// <param name="Code">The launch code ('SND-LAUNCH-0…').</param>
/// <param name="Reason">The refusal's reason, or null.</param>
/// <param name="Source">The credential source for -017, otherwise null.</param>
/// <param name="Detail">The launcher's message, scrubbed.</param>
public sealed record LaunchRefused(string Actor, RunToken Run, string Code, string? Reason, string? Source, string Detail) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => TriggerNames.LaunchRefused;

    /// <inheritdoc/>
    public override string KeyPart => "launch.refused:run:" + Run.Value;
}
