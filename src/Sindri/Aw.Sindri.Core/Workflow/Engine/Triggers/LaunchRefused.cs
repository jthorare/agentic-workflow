namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>The launch was refused so that the instance parks as 'launch' (SND-LAUNCH-015, -016; sandbox spec SB16): the park's Resume starts a new run.</summary>
public sealed record LaunchRefused(string Actor, RunToken Run, string Code, string? Reason, string? Source, string Detail) : LaunchRefusal(Actor, Run, Code, Reason, Source, Detail)
{
    /// <inheritdoc/>
    public override string Name => TriggerNames.LaunchRefused;

    /// <inheritdoc/>
    public override string KeyPart => "launch.refused:run:" + Run.Value;
}
