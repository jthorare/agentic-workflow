namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): from a failed run to the step that failed.</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>Opens the workflow on the step the failed run stopped at: back to the workflow's graph with that step selected, and its settings open when the workflow can be edited.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the step is selected (and its sheet open).</returns>
    internal async Task OpenFailedStep(CancellationToken cancellationToken)
    {
        if (Run.Failure is not { } failure)
        {
            return;
        }

        Back();
        if (Runs.Graph is { } graph && graph.SelectNode(failure.Node) && CanOpenSettings)
        {
            await OpenSettings(cancellationToken).ConfigureAwait(true);
        }
    }
}
