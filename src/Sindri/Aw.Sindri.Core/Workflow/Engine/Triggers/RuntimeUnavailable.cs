namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>The launch found no agent runtime on this box (SND-LAUNCH-014, -017; sandbox spec SB16, N1): a deskCapable Step runs at the desk in the same frame, with no round counted and no Step failure; any other Step parks 'SND-WF-010' with the sandbox marker.</summary>
public sealed record RuntimeUnavailable(string Actor, RunToken Run, string Code, string? Reason, string? Source, string Detail) : LaunchRefusal(Actor, Run, Code, Reason, Source, Detail)
{
    /// <inheritdoc/>
    public override string Name => TriggerNames.RuntimeUnavailable;

    /// <inheritdoc/>
    public override string KeyPart => "runtime.unavailable:run:" + Run.Value;
}
