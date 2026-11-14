using System.Collections.ObjectModel;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// One workflow's runs, newest first as the API serves them, under the workflow's name and one line on the revision shown. A workflow with no active revision has no
/// runs list (Sindri refuses it with SND-GRAPH-105); the page says so in its own words instead of showing an error.
/// </summary>
internal sealed class RunsViewModel(ISindriApi api, TimeProvider clock) : ObservableObject
{
    /// <summary>The refusal code for a workflow with no active revision.</summary>
    internal const string NotActive = "SND-GRAPH-105";
    private int _reads;
    private List<RunRowViewModel> _all = [];
    private readonly HashSet<string> _ticked = new(StringComparer.Ordinal);   // ticked run ids survive a re-read
    private string _status = string.Empty;
    private string _range = string.Empty;

    /// <summary>The workflow's slug.</summary>
    public string Slug { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The workflow's name.</summary>
    public string Title { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Revision 1 of 2 · 2 nodes · created by owner" ("2 nodes · created by you" with no active revision); empty when the workflow could not be read.</summary>
    public string DetailText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"1 running · 3 runs"; empty until the first answer.</summary>
    public string Head { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The shown revision as a graph at the positions Sindri served, or null until the workflow is read.</summary>
    public WorkflowGraphViewModel? Graph { get; private set => Set(ref field, value); }

    /// <summary>The workflow as last read, or null: run detail draws its graph when the run walked this revision.</summary>
    internal WorkflowView? Shown { get; private set => Set(ref field, value); }

    /// <summary>The runs, newest first.</summary>
    public ObservableCollection<RunRowViewModel> Rows { get; } = [];

    /// <summary>"Reading runs…" until the first answer.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string ReadingText => Labels.WfRunsReading;

    /// <summary>The retry action's words.</summary>
    public string RetryText => Labels.WfTryAgain;
#pragma warning restore CA1822, MA0041

    /// <summary>The Run now button's words: "Run now", or "Starting…" while its call is on the way.</summary>
    public string RunNowText => IsStarting ? Labels.WfRunNowBusy : Labels.WfRunNow;

    /// <summary>True while Run now's call is on the way; a second press does nothing.</summary>
    public bool IsStarting { get; private set { Set(ref field, value); OnChanged(nameof(RunNowText)); OnChanged(nameof(CanPressRunNow)); } }

    /// <summary>True when the workflow has an active revision to run: false until the runs read lands, and once it or Run now said the workflow is off.</summary>
    public bool CanRun { get; private set { Set(ref field, value); OnChanged(nameof(CanPressRunNow)); } }

    /// <summary>True when Run now can be pressed: the workflow is on and no call is on the way.</summary>
    public bool CanPressRunNow => CanRun && !IsStarting;

    /// <summary>"23 of 500 runs" with a filter on, "500 runs" without.</summary>
    public string CountText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when a status or date filter is on.</summary>
    public bool HasFilter => !string.IsNullOrEmpty(_status) || !string.IsNullOrEmpty(_range);

    /// <summary>True when a filter leaves no run.</summary>
    public bool HasNoMatch => HasFilter && _all.Count > 0 && Rows.Count == 0;

    /// <summary>The line when a filter leaves no run.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string NoMatchText => Labels.WfRunsNoMatch;
#pragma warning restore CA1822, MA0041

    /// <summary>How many runs are ticked.</summary>
    public int SelectedCount => _all.Count(r => r.IsSelected);

    /// <summary>True when exactly two runs are ticked.</summary>
    public bool CanCompare => SelectedCount == 2;

    /// <summary>What to do when the ticks do not make two: "Tick one more run to compare" or "Compare takes two runs: untick one"; empty otherwise.</summary>
    public string CompareHint => string.Empty;

    /// <summary>"Compare 2".</summary>
    public string CompareText => Labels.WfCompare(2);

    /// <summary>The status filter's choices.</summary>
    public IReadOnlyList<RunFilterChoice> StatusChoices { get; } = [new("", "All statuses"), new("failed", "Failed"), new("succeeded", "Succeeded"), new("running", "Running"), new("cancelled", "Cancelled")];

    /// <summary>The date filter's choices.</summary>
    public IReadOnlyList<RunFilterChoice> DateChoices { get; } = [new("", "Any time"), new("24h", "Last 24 hours"), new("7d", "Last 7 days"), new("30d", "Last 30 days")];

    /// <summary>The picked status choice; picking one filters.</summary>
    public RunFilterChoice SelectedStatus { get => StatusChoices.First(c => string.Equals(c.Key, _status, StringComparison.Ordinal)); set => SetStatusFilter(value?.Key); }

    /// <summary>The picked date choice; picking one filters.</summary>
    public RunFilterChoice SelectedDate { get => DateChoices.First(c => string.Equals(c.Key, _range, StringComparison.Ordinal)); set => SetDateFilter(value?.Key); }

    /// <summary>The status filter: "failed", "succeeded", "running", "cancelled", or empty for every status.</summary>
    public string StatusFilter => _status;

    /// <summary>The date filter: "24h", "7d", "30d", or empty for every date.</summary>
    public string DateFilter => _range;

    /// <summary>Filters by status ("failed", "succeeded", "running", "cancelled"); null or empty shows every status.</summary>
    /// <param name="status">The status.</param>
    internal void SetStatusFilter(string? status)
    {
        _status = status ?? string.Empty;
        Apply();
    }

    /// <summary>Filters by date ("24h", "7d", "30d"); null or empty shows every date.</summary>
    /// <param name="range">The range.</param>
    internal void SetDateFilter(string? range)
    {
        _range = range ?? string.Empty;
        Apply();
    }

    /// <summary>Clears both filters.</summary>
    internal void ClearFilters()
    {
        (_status, _range) = (string.Empty, string.Empty);
        Apply();
    }

    // The rows the filters keep, newest first as served; the count says how many of all.
    private void Apply()
    {
        var since = _range switch { "24h" => TimeSpan.FromHours(24), "7d" => TimeSpan.FromDays(7), "30d" => TimeSpan.FromDays(30), _ => TimeSpan.Zero };
        var now = clock.GetUtcNow();
        var kept = _all.Where(r => (_status.Length == 0 || string.Equals(r.Status, _status, StringComparison.Ordinal)) && (since == TimeSpan.Zero || now - r.StartedAt <= since)).ToList();
        Patch.To(Rows, kept);
        CountText = HasFilter ? Labels.WfRunsCountOf(kept.Count, _all.Count) : Labels.WfRunsCount(_all.Count);
        OnChanged(nameof(HasFilter));
        OnChanged(nameof(HasNoMatch));
        OnChanged(nameof(StatusFilter));
        OnChanged(nameof(DateFilter));
        OnChanged(nameof(SelectedStatus));
        OnChanged(nameof(SelectedDate));
        OnSelectionChanged();
    }

    private void OnSelectionChanged()
    {
        OnChanged(nameof(SelectedCount));
        OnChanged(nameof(CanCompare));
    }

    /// <summary>What the last Run now said (queued, skipped or refused), in words; empty otherwise.</summary>
    public string NoticeText { get; private set { Set(ref field, value); OnChanged(nameof(HasNotice)); } } = string.Empty;

    /// <summary>True when there is a note under the Run now button.</summary>
    public bool HasNotice => NoticeText.Length > 0;

    /// <summary>True until the answer.</summary>
    public bool IsReading { get; private set => Set(ref field, value); }

    /// <summary>True when there are no runs to list.</summary>
    public bool IsEmpty { get; private set => Set(ref field, value); }

    /// <summary>"No runs yet." or "This workflow is off, so it has no runs."; empty otherwise.</summary>
    public string EmptyText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the read failed.</summary>
    public bool IsFailed { get; private set => Set(ref field, value); }

    /// <summary>The sentence for a failed read.</summary>
    public string FailedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Reads a workflow and its runs together and shows them. The workflow's own read only fills the line under the name, so its failure shows nothing there.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="slug">The workflow.</param>
    /// <param name="name">The workflow's name, as the list showed it.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the runs are shown or the failure is.</returns>
    internal async Task Read(Uri url, string slug, string name, CancellationToken cancellationToken)
    {
        var read = ++_reads;
        if (!string.Equals(Slug, slug, StringComparison.Ordinal))
        {
            (_all, _status, _range) = ([], string.Empty, string.Empty);
            _ticked.Clear();
            Rows.Clear();   // another workflow: none of the old runs stay
            (Head, DetailText) = (string.Empty, string.Empty);
            (IsFailed, FailedText, IsEmpty, EmptyText) = (false, string.Empty, false, string.Empty);   // nor does the old one's failure or "off" line
            (CanRun, NoticeText) = (false, string.Empty);   // until this one's runs read says it is on
            (Graph, Shown) = (null, null);
        }

        (Slug, Title) = (slug, name);
        IsReading = Rows.Count == 0;
        if (IsReading)
        {
            (IsFailed, FailedText) = (false, string.Empty);   // the old failure line hides while this read is on its way
        }

        var detailRead = api.Workflow(url, slug, cancellationToken);
        var runsRead = api.WorkflowRuns(url, slug, cancellationToken);
        await Task.WhenAll(detailRead, runsRead).ConfigureAwait(true);
        var (detail, runs) = (await detailRead.ConfigureAwait(true), await runsRead.ConfigureAwait(true));
        if (read != _reads)
        {
            return;   // a newer read was asked for since
        }

        Land(detail, runs);
    }

    // What the two reads said: the line under the name and the graph from the workflow's read, the list (or "off", or the failure) from the runs read.
    private void Land(Result<WorkflowView> detail, Result<WorkflowRunList> runs)
    {
        IsReading = false;
        DetailText = detail.IsSuccess ? Detail(detail.Value) : string.Empty;
        (Shown, Graph) = detail.IsSuccess ? (detail.Value, WorkflowGraphViewModel.Keeping(Graph, new WorkflowGraphViewModel(detail.Value))) : (Shown, Graph);
        if (runs.IsSuccess)
        {
            CanRun = true;
            Show(runs.Value);
        }
        else if (string.Equals(runs.Error.Code, NotActive, StringComparison.Ordinal))
        {
            Rows.Clear();
            (IsFailed, FailedText, IsEmpty, EmptyText, Head) = (false, string.Empty, true, Labels.WfRunsOff, string.Empty);
            CanRun = false;
        }
        else
        {
            (IsFailed, FailedText, IsEmpty, EmptyText) = (true, Labels.WfRunsFailed(runs.Error), false, string.Empty);
        }
    }

    /// <summary>Turns Run now off: Sindri said the workflow has no active revision.</summary>
    internal void TurnOff() => CanRun = false;

    /// <summary>Marks Run now's call as on the way, or as done.</summary>
    /// <param name="starting">True while the call is on the way.</param>
    internal void SetStarting(bool starting) => IsStarting = starting;

    /// <summary>Shows what Run now said, in words.</summary>
    /// <param name="text">The note; empty clears it.</param>
    internal void Note(string text) => NoticeText = text;

    private void Show(WorkflowRunList list)
    {
        (IsFailed, FailedText, CanRun) = (false, string.Empty, true);
        _all = [.. list.Runs.Select(run => new RunRowViewModel(run, clock) { IsSelected = _ticked.Contains(run.Id) })];
        foreach (var row in _all)
        {
            row.PropertyChanged += (_, e) =>
            {
                if (string.Equals(e.PropertyName, nameof(RunRowViewModel.IsSelected), StringComparison.Ordinal))
                {
                    _ = row.IsSelected ? _ticked.Add(row.Id) : _ticked.Remove(row.Id);
                    OnSelectionChanged();
                }
            };
        }

        Apply();

        var running = list.Runs.Count(r => string.Equals(r.Status, "running", StringComparison.Ordinal));
        Head = list.Runs.Count == 0 ? Labels.WfRunsHeadNone : Labels.WfRunsHead(running, list.Runs.Count);
        (IsEmpty, EmptyText) = list.Runs.Count == 0 ? (true, Labels.WfRunsNone) : (false, string.Empty);
    }

    private static string Detail(WorkflowView workflow)
    {
        var nodes = workflow.Spec.ValueKind == JsonValueKind.Object && workflow.Spec.TryGetProperty("nodes", out var list) && list.ValueKind == JsonValueKind.Array ? list.GetArrayLength() : 0;
        return Labels.WfDetail(workflow.Revision, workflow.LatestRevision, nodes, workflow.CreatedBy, workflow.ActiveRevision is not null);
    }
}
