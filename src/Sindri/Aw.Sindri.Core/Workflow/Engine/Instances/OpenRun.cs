namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>A frame's open Step run (spec §5.1): only a report with its token is current; a suspension leaves it open.</summary>
/// <param name="Token">'{entry}.{n}'.</param>
/// <param name="Runtime">Desk or agent.</param>
/// <param name="Launch">The agent launch ('LCH-…'), for an agent run.</param>
/// <param name="Task">The desk task the human sees, for a desk run.</param>
/// <param name="Escalation">0 for the first launch of an agent run, 1 once it was retried on a stronger model (model-pick spec 5, 7.1); the retry's launch id differs.</param>
public sealed record OpenRun(RunToken Token, StepRuntime Runtime, string? Launch, string? Task, int Escalation = 0)
{
    /// <summary>How many failed runs the engine has retried in a row for this entry (the bounded retry before a Step parks as unavailable); 0 for a run nothing has failed under. A human's rerun starts again from 0.</summary>
    public int Retries { get; init; }
}
