namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>A Step block (spec §2): the work of an active state. Registered with AddStep.</summary>
public interface IStep
{
    /// <summary>Starts a run, after the Commit that opened it; keyed by the run token, so a re-run after a crash is a no-op.</summary>
    /// <param name="context">What the run starts with.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>A task that completes once the run has started.</returns>
    public ValueTask Start(StepContext context, CancellationToken cancellationToken);

    /// <summary>Completes a run from its report.</summary>
    /// <param name="run">The run.</param>
    /// <param name="report">What it reported.</param>
    /// <param name="cancellationToken">Cancels the completion.</param>
    /// <returns>The result the trigger carries.</returns>
    public ValueTask<StepResult> Complete(StepRun run, StepReport report, CancellationToken cancellationToken);
}
