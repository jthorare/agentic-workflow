using System.Collections.ObjectModel;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Jobs;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Jobs;

/// <summary>
/// The Jobs page (API P6): the jobs Sindri ran in the background, newest first as the API serves them, each with its state, who started it, when,
/// how it ended and Cancel while it runs. The sentence for an empty list is the API's. The page reads while it is the page shown, on every event
/// (job.started, job.progress, job.finished and job.cancelled all arrive on the stream), and keeps its rows by id across reads.
/// </summary>
internal sealed partial class JobsPageViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<string, JobRowViewModel> _rows = new(StringComparer.Ordinal);
    private int _reads;
    private CancellationTokenSource? _shown;
    private Uri? _url;
    private readonly CatalogViewModel _catalog;
    private readonly TimeProvider _clock;

    /// <summary>The page over its ports.</summary>
    /// <param name="api">The API.</param>
    /// <param name="catalog">The command catalog, for the Cancel control.</param>
    /// <param name="clock">The clock the ages run on.</param>
    internal JobsPageViewModel(ISindriApi api, CatalogViewModel catalog, TimeProvider clock)
    {
        (Api, _catalog, _clock) = (api, catalog, clock);
        catalog.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(CatalogViewModel.Controls), StringComparison.Ordinal))
            {
                foreach (var row in _rows.Values)
                {
                    row.Recatalog();   // a catalog read after the rows were drawn gives the running ones their Cancel
                }
            }
        };
    }

    /// <summary>Raised when a row asks to open its job's sheet; the shell opens it.</summary>
    internal event EventHandler<JobOpenRequestedEventArgs>? OpenRequested;

    /// <summary>The API the page reads.</summary>
    internal ISindriApi Api { get; }

    /// <summary>Asks the shell to open a job's sheet.</summary>
    /// <param name="link">The job's link.</param>
    internal void RequestOpen(DeepLink link) => OpenRequested?.Invoke(this, new JobOpenRequestedEventArgs(link));

    /// <summary>The page's name.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string Title => Labels.JobsPage;

    /// <summary>What the page is for, in one line.</summary>
    public string Purpose => Labels.JobsPurpose;

    /// <summary>The stale note's words.</summary>
    public string StaleText => Labels.Stale;

    /// <summary>What the page says when an answer can't be read.</summary>
    public string UnreadableText => Labels.JobsUnreadable;
#pragma warning restore CA1822, MA0041

    /// <summary>The jobs, as the API orders them.</summary>
    public ObservableCollection<JobRowViewModel> Rows { get; } = [];

    /// <summary>"2 running · 5 jobs"; empty until the first list.</summary>
    public string Head { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The head's accessible name.</summary>
    public string HeadName => Head.Length > 0 ? Title + ", " + Head : Title;

    /// <summary>"Reading jobs…" until the first answer.</summary>
    public string ReadingText { get; } = Labels.ReadingJobs;

    /// <summary>True until the first answer.</summary>
    public bool IsReading { get; private set => Set(ref field, value); } = true;

    /// <summary>True when the list is empty.</summary>
    public bool IsEmpty { get; private set => Set(ref field, value); }

    /// <summary>The API's sentence for an empty list; empty otherwise.</summary>
    public string EmptyText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while the answer can't be read by this build.</summary>
    public bool IsUnreadable { get; private set => Set(ref field, value); }

    /// <summary>True while the rows shown are the last good ones (a read failed).</summary>
    public bool IsStale { get; private set => Set(ref field, value); }

    /// <summary>True when the first read failed and there are no rows to show.</summary>
    public bool IsFailed { get; private set => Set(ref field, value); }

    /// <summary>The sentence for a failed first read.</summary>
    public string FailedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The retry action's words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string RetryText => Labels.TryAgain;

    /// <summary>Reads again after a failed first read.</summary>
#pragma warning restore CA1822, MA0041
    internal Task Retry(CancellationToken cancellationToken) => _url is { } url ? Read(url, cancellationToken) : Task.CompletedTask;

    /// <summary>The catch-up read started by the last show.</summary>
    internal Task CatchUp { get; private set; } = Task.CompletedTask;

    /// <summary>How many lists this page has drawn (a test waits on it).</summary>
    internal int Drawn { get; private set; }

    /// <summary>True while the page is the one shown.</summary>
    internal bool IsActive { get; private set; }

    /// <summary>Re-reads the list (on connect and on every event). While the page is not the one shown it only remembers where the API is.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        return IsActive ? Read(url, cancellationToken) : Task.CompletedTask;
    }

    /// <summary>The page was shown or hidden (the shell's page changed). Becoming the page shown reads at once.</summary>
    /// <param name="shown">True while the Jobs page is the page.</param>
    internal void SetShown(bool shown)
    {
        var was = IsActive;
        IsActive = shown;
        if (shown && !was && _url is { } url)
        {
            foreach (var row in _rows.Values)
            {
                row.Age();
            }

            _shown = new CancellationTokenSource();
            CatchUp = CatchUpRead(url, _shown.Token);
        }
        else if (!shown)
        {
            _reads++;   // a read already on its way is not wanted
            Dispose();
        }
    }

    /// <summary>Cancels the read the page started when it was shown.</summary>
    public void Dispose()
    {
        _shown?.Cancel();
        _shown?.Dispose();
        _shown = null;
    }

    // The read made when the page is shown, awaited by CatchUp so nothing about it is unobserved: a leave cancels it, any other failure is drawn as a failed read.
    private async Task CatchUpRead(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            await Read(url, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // the page was left
        }
        catch (HttpRequestException e)
        {
            Fail(SindriUiErrors.Unreachable.With(e.Message));
        }
    }

    private async Task Read(Uri url, CancellationToken cancellationToken)
    {
        var read = ++_reads;
        var list = await Api.Jobs(url, Query(), cancellationToken).ConfigureAwait(true);
        if (read != _reads)
        {
            return;   // a newer read was asked for since, or the page was left: its answer is the one to show
        }

        if (list.IsSuccess)
        {
            Show(list.Value);
            return;
        }

        Fail(list.Error);
    }

    private void Fail(Error error)
    {
        var unreadable = string.Equals(error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal);
        (IsReading, IsUnreadable, IsStale) = (false, unreadable, true);   // the last good rows stay, marked stale (screens §4.1)
        IsFailed = !unreadable && Rows.Count == 0;   // with nothing to show, the page says so and offers to try again
        FailedText = IsFailed ? Labels.JobsFailed(error) : string.Empty;
    }

    private void Show(JobList list)
    {
        Drawn++;
        (IsReading, IsUnreadable, IsStale, IsFailed, FailedText) = (false, false, false, false, string.Empty);
        var rows = list.Jobs.Select(Row).ToList();
        var present = new HashSet<string>(list.Jobs.Select(j => j.Id), StringComparer.Ordinal);
        foreach (var gone in _rows.Keys.Where(k => !present.Contains(k)).ToList())
        {
            _ = _rows.Remove(gone);
        }

        Patch.To(Rows, rows);
        Head = Labels.JobsHead(list.Jobs.Count(j => string.Equals(j.State, "running", StringComparison.Ordinal)), list.Jobs.Count);
        (IsEmpty, EmptyText) = (rows.Count == 0, rows.Count == 0 ? list.Empty ?? string.Empty : string.Empty);
        IsNoneRunning = rows.Count > 0 && !rows.Exists(r => r.IsRunning);
        OnChanged(nameof(HeadName));
        NoteKinds(list.Jobs);
    }

    private JobRowViewModel Row(JobRowView served)
    {
        if (_rows.TryGetValue(served.Id, out var kept))
        {
            kept.Update(served);
            return kept;
        }

        return _rows[served.Id] = new JobRowViewModel(served, Cancel, _clock);
    }

    // Cancel is the catalog's CancelJob row acting on this job; the app sends it once and never retries. The catalog may not be read yet: the row then has none until the next read.
    private ControlViewModel? Cancel(string id, string title) =>
        _catalog.On("CancelJob", title, ct => _url is { } url ? Api.CancelJob(url, id, ct) : Task.FromResult<Result<CommandAcceptedView>>(SindriUiErrors.Unreachable.With("Sindri isn't reachable yet")));
}
