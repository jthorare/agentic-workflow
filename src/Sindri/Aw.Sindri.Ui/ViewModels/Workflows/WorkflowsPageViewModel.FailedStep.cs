namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): from a failed run to the step that failed.</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>Opens the workflow on the step the failed run stopped at. Not built yet.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the step is open.</returns>
    internal Task OpenFailedStep(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = IsRuns;   // stub: nothing opens yet
        return Task.CompletedTask;
    }
}
