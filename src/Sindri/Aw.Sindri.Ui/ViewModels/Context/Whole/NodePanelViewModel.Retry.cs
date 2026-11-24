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
    internal Task Retry(CancellationToken cancellationToken = default) => IsFailed && Key is { } key ? Open(key, cancellationToken) : Task.CompletedTask;

    /// <summary>Gets a value indicating whether the refused launch can be tried again (the "Try again" button; Copy path stays beside it).</summary>
    public bool CanRetryLaunch => _launch.CanRetry;

    /// <summary>Gets the "Try again" button's words.</summary>
    public string RetryLaunchText { get; } = Labels.ContextPagesRetry;

    /// <summary>Gets the "Try again" button's accessible name.</summary>
    public string RetryLaunchName { get; } = Labels.ContextLaunchRetryName;

    /// <summary>Repeats the launch the system refused.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>When the system answered.</returns>
    internal Task RetryLaunch(CancellationToken cancellationToken = default) => _launch.Retry(cancellationToken);
}
