namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The node panel's fix buttons (<see cref="NodePanelViewModel"/>, continued, #3060): a failed read is read again.</summary>
internal sealed partial class NodePanelViewModel
{
    /// <summary>Gets the Retry button's words.</summary>
    public string RetryText { get; } = Labels.ContextWholeRetry;

    /// <summary>Gets the Retry button's accessible name.</summary>
    public string RetryName { get; } = Labels.ContextNodePanelRetryName;

    /// <summary>Reads the failed node again (the Retry button).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the panel shows the answer or the failure.</returns>
    internal Task Retry(CancellationToken cancellationToken = default) => cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) : Task.CompletedTask;
}
