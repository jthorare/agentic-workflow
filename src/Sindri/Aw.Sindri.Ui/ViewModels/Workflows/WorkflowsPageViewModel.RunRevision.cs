namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): "Run revision N once", one real run of a saved revision that is not live (#3691).</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>True when the button shows: a saved revision is newer than the live one.</summary>
    public bool ShowRunRevisionOnce => IsNotLive && Runs.IsStarting;   // red stub: never shown while idle

    /// <summary>The button's words, "Run revision N once".</summary>
    public string RunRevisionOnceText => SavedLineText;   // red stub: not the button's words

    /// <summary>Runs the newest saved revision once; the live revision stays.</summary>
    /// <param name="cancellationToken">Cancels the call and the reads.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task RunRevisionOnce(CancellationToken cancellationToken) => RunNow(cancellationToken);   // red stub: a plain run, not the revision
}
