namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): "Run revision N once", one real run of a saved revision that is not live (#3691).</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>True when the button shows: a saved revision is newer than the live one ("Saved as revision N. Not live yet."). Run now covers the live one.</summary>
    public bool ShowRunRevisionOnce => IsNotLive;

    /// <summary>The button's words, "Run revision N once", N the newest saved revision.</summary>
    public string RunRevisionOnceText => Runs.Shown is { } shown ? Labels.WfRunRevisionOnce(shown.LatestRevision) : string.Empty;

    /// <summary>
    /// Runs the newest saved revision once; the live revision stays. Sent once, like Run now: a started run is opened, a queued or skipped one says so,
    /// and a refusal shows where Run now's does. Nothing happens unless the button shows and Run now could run.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call and the reads.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task RunRevisionOnce(CancellationToken cancellationToken) =>
        ShowRunRevisionOnce && Runs.Shown is { } shown ? StartRun(shown.LatestRevision, cancellationToken) : Task.CompletedTask;
}
