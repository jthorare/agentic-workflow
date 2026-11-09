using System.Collections.ObjectModel;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The Workflows page, read-only (#2258 PR 17 part 1): the workflows with their state and who changed them, one workflow's runs, and one run's detail. The three levels
/// share the page; Back goes up one. The page reads over HTTP only, when it is shown and when a level is opened, and keeps no logic of Sindri's.
/// </summary>
internal sealed class WorkflowsPageViewModel : ObservableObject
{
    private enum Level
    {
        List,
        Runs,
        Run,
    }

    private const string RunAlreadyEnded = "SND-GRAPH-107";

    private readonly TimeProvider _clock;
    private int _reads;
    private Uri? _url;
    private Level _level = Level.List;

    /// <summary>The page over its ports.</summary>
    /// <param name="api">The API.</param>
    /// <param name="clock">The clock the ages run on.</param>
    internal WorkflowsPageViewModel(ISindriApi api, TimeProvider clock)
    {
        (Api, _clock) = (api, clock);
        Runs = new RunsViewModel(api, clock);
        Run = new RunDetailViewModel(api, clock);
    }

    /// <summary>The API the page reads.</summary>
    internal ISindriApi Api { get; }

    /// <summary>The page's name.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string Title => Labels.Workflows;

    /// <summary>What the page is for, in one line.</summary>
    public string Purpose => Labels.WfPurpose;

    /// <summary>"Reading workflows…" until the first answer.</summary>
    public string ReadingText => Labels.WfReading;

    /// <summary>What the page says when an answer can't be read.</summary>
    public string UnreadableText => Labels.WfUnreadable;

    /// <summary>The retry action's words.</summary>
    public string RetryText => Labels.WfTryAgain;

    /// <summary>The way back up a level.</summary>
    public string BackText => Labels.WfBack;
#pragma warning restore CA1822, MA0041

    /// <summary>The workflows, by slug as the API serves them.</summary>
    public ObservableCollection<WorkflowRowViewModel> Rows { get; } = [];

    /// <summary>"2 workflows · 1 active"; empty until the first list.</summary>
    public string Head { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True until the first answer.</summary>
    public bool IsReading { get; private set => Set(ref field, value); } = true;

    /// <summary>True when the list is empty.</summary>
    public bool IsEmpty { get; private set => Set(ref field, value); }

    /// <summary>The sentence for an empty list; empty otherwise.</summary>
    public string EmptyText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while the answer can't be read by this build.</summary>
    public bool IsUnreadable { get; private set => Set(ref field, value); }

    /// <summary>True when the last read of the list failed (not unreadable); the old rows, if any, stay under the line.</summary>
    public bool IsFailed { get; private set => Set(ref field, value); }

    /// <summary>The sentence for a failed read.</summary>
    public string FailedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True on the list of workflows.</summary>
    public bool IsList => _level == Level.List;

    /// <summary>True on one workflow's runs.</summary>
    public bool IsRuns => _level == Level.Runs;

    /// <summary>True on one run's detail.</summary>
    public bool IsRun => _level == Level.Run;

    /// <summary>The open workflow's runs.</summary>
    public RunsViewModel Runs { get; }

    /// <summary>The open run.</summary>
    public RunDetailViewModel Run { get; }

    /// <summary>The page was shown: reads the list, and the level open under it again.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when what the page shows is read.</returns>
    internal async Task Show(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        await ReadList(url, cancellationToken).ConfigureAwait(true);
        if (_level == Level.Runs)
        {
            await Runs.Read(url, Runs.Slug, Runs.Title, cancellationToken).ConfigureAwait(true);
        }
        else if (_level == Level.Run)
        {
            await Run.Read(url, Run.Id, cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Reads the list again (the Try again action).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown.</returns>
    internal Task Retry(CancellationToken cancellationToken) => _url is { } url ? ReadList(url, cancellationToken) : Task.CompletedTask;

    /// <summary>Reads the open workflow's runs again (the runs level's Try again).</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the runs are shown or the failure is.</returns>
    internal Task RetryRuns(CancellationToken cancellationToken) => _url is { } url ? Runs.Read(url, Runs.Slug, Runs.Title, cancellationToken) : Task.CompletedTask;

    /// <summary>Reads the open run again (the run level's Try again).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the run is shown or the failure is.</returns>
    internal Task RetryRun(CancellationToken cancellationToken) => _url is { } url ? Run.Read(url, Run.Id, cancellationToken) : Task.CompletedTask;

    /// <summary>
    /// Run now: starts a run of the open workflow by hand. The call is sent once (a human action, never retried) and a second press while it is on the way does nothing. A run that
    /// started is opened; a queued or skipped one says so under the button; a refusal shows its message. The runs are read again after any answer that reached Sindri.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call and the reads.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task RunNow(CancellationToken cancellationToken)
    {
        if (_url is not { } url || Runs.IsStarting || !Runs.CanRun || Runs.Slug.Length == 0)
        {
            return;
        }

        var slug = Runs.Slug;
        Runs.SetStarting(true);
        Runs.Note(string.Empty);
        try
        {
            var started = await Api.StartWorkflowRun(url, slug, cancellationToken).ConfigureAwait(true);
            if (started.IsError)
            {
                Runs.Note(Labels.WfRunNowRefused(started.Error));
                return;
            }

            if (!string.Equals(Runs.Slug, slug, StringComparison.Ordinal))
            {
                return;   // the page moved to another workflow meanwhile: its note and list are not this answer's
            }

            await Runs.Read(url, slug, Runs.Title, cancellationToken).ConfigureAwait(true);
            switch (started.Value.Status)
            {
                case "started":
                    if (_level == Level.Runs)
                    {
                        await OpenRun(started.Value.Run, cancellationToken).ConfigureAwait(true);
                    }

                    break;
                case "queued":
                    Runs.Note(Labels.WfRunQueued(started.Value.Reason));
                    break;
                default:
                    Runs.Note(Labels.WfRunSkipped(started.Value.Reason));
                    break;
            }
        }
        finally
        {
            Runs.SetStarting(false);
        }
    }

    /// <summary>
    /// Cancel run: asks the open run to stop. Sent once. "Already ended" is shown in words and the run is read again, so its chip says how it ended; a delivered cancel is read again
    /// too, and the run ends as cancelled when its workflow stops.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call and the read.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task CancelRun(CancellationToken cancellationToken)
    {
        if (_url is not { } url || Run.IsCancelling || !Run.CanCancel)
        {
            return;
        }

        var id = Run.Id;
        Run.SetCancelling(true);
        Run.Note(string.Empty);
        try
        {
            var cancelled = await Api.CancelWorkflowRun(url, id, cancellationToken).ConfigureAwait(true);
            var ended = cancelled.IsError && string.Equals(cancelled.Error.Code, RunAlreadyEnded, StringComparison.Ordinal);
            var note = cancelled.IsSuccess ? Labels.WfCancelSent : ended ? Labels.WfCancelEnded : Labels.WfCancelRefused(cancelled.Error);
            if (!string.Equals(Run.Id, id, StringComparison.Ordinal))
            {
                return;   // another run is open now
            }

            Run.Note(note);
            if (cancelled.IsSuccess || ended)
            {
                await Run.Read(url, id, cancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            Run.SetCancelling(false);
        }
    }

    /// <summary>The page was left: it returns to its list and a read on its way is not wanted.</summary>
    internal void Leave()
    {
        _reads++;
        SetLevel(Level.List);
    }

    /// <summary>Opens a workflow's runs.</summary>
    /// <param name="slug">The workflow.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the runs are shown.</returns>
    internal Task OpenWorkflow(string slug, CancellationToken cancellationToken)
    {
        SetLevel(Level.Runs);
        var name = Rows.FirstOrDefault(r => string.Equals(r.Slug, slug, StringComparison.Ordinal))?.NameText ?? slug;
        return _url is { } url ? Runs.Read(url, slug, name, cancellationToken) : Task.CompletedTask;
    }

    /// <summary>Opens a run's detail.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the run is shown.</returns>
    internal Task OpenRun(string runId, CancellationToken cancellationToken)
    {
        SetLevel(Level.Run);
        return _url is { } url ? Run.Read(url, runId, cancellationToken) : Task.CompletedTask;
    }

    /// <summary>Goes up one level: a run to its workflow's runs, runs to the list.</summary>
    internal void Back() => SetLevel(_level == Level.Run ? Level.Runs : Level.List);

    private void SetLevel(Level level)
    {
        if (_level == level)
        {
            return;
        }

        _level = level;
        OnChanged(nameof(IsList));
        OnChanged(nameof(IsRuns));
        OnChanged(nameof(IsRun));
    }

    private async Task ReadList(Uri url, CancellationToken cancellationToken)
    {
        var read = ++_reads;
        var list = await Api.Workflows(url, cancellationToken).ConfigureAwait(true);
        if (read != _reads)
        {
            return;   // a newer read was asked for since, or the page was left: its answer is the one to show
        }

        if (list.IsSuccess)
        {
            ShowList(list.Value);
            return;
        }

        Fail(list.Error);
    }

    private void Fail(Error error)
    {
        var unreadable = string.Equals(error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal);
        (IsReading, IsUnreadable) = (false, unreadable);
        (IsFailed, FailedText) = unreadable ? (false, string.Empty) : (true, Labels.WfFailed(error));
    }

    private void ShowList(WorkflowList list)
    {
        (IsReading, IsUnreadable, IsFailed, FailedText) = (false, false, false, string.Empty);
        var rows = list.Workflows.Select(w => new WorkflowRowViewModel(w, _clock)).ToList();
        Patch.To(Rows, rows);
        Head = rows.Count == 0 ? Labels.WfHeadNone : Labels.WfHead(rows.Count, rows.Count(r => r.IsActive));
        (IsEmpty, EmptyText) = rows.Count == 0 ? (true, Labels.WfNone) : (false, string.Empty);
    }
}
