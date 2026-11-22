using System.ComponentModel;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the server's validation findings (#3803). Each time the open workflow's read brings a new revision, the page asks
/// POST /sindri/workflows/validate (the workflow's newest revision) and shows what came back: a mark on each step's card, a count over the graph, and the selected step's findings with
/// the server's sentence and fix line. GRAPH-PROMPT-001 has a fix button: it opens the step's sheet on its question field and asks for the <c>/</c> field picker, which #3033 builds.
/// </summary>
internal sealed partial class WorkflowsPageViewModel
{
    private int _findingReads;   // counts validate calls: only the newest one's answer shows
    private (string Slug, int Latest, string Hash)? _findingsOf;   // the read the findings were asked for: a re-read of the same revision asks nothing
    private WorkflowGraphViewModel? _findingsGraph;   // the graph whose selection picks the step's findings

    /// <summary>The open workflow's findings.</summary>
    public WorkflowFindingsViewModel Findings { get; } = new();

    /// <summary>The findings of the step selected on the graph, in the server's order; empty when none is selected or it has none.</summary>
    public IReadOnlyList<WorkflowFindingViewModel> StepFindings { get; private set { Set(ref field, value); OnChanged(nameof(HasStepFindings)); OnChanged(nameof(ShowsStepFindingsUnderGraph)); } } = [];

    /// <summary>True when the selected step has findings (the sheet lists them).</summary>
    public bool HasStepFindings => StepFindings.Count > 0;

    /// <summary>True when the selected step's findings show under the graph: it has some and the sheet, which lists them itself, is shut.</summary>
    public bool ShowsStepFindingsUnderGraph => HasStepFindings && !IsSettingsOpen;

    /// <summary>The config field the sheet focuses after a fix (such as "prompt"), or null; forgotten when the sheet closes.</summary>
    public string? FocusFieldName { get; private set => Set(ref field, value); }

    /// <summary>True when a fix asked for the <c>/</c> field picker on <see cref="FocusFieldName"/>. The picker does not exist yet: #3033 reads this flag to open it.</summary>
    public bool IsFieldPickerAsked { get; private set => Set(ref field, value); }

    /// <summary>The newest validate call (tests await it); a completed task before the first.</summary>
    internal Task FindingsRead { get; private set; } = Task.CompletedTask;

    /// <summary>Runs a finding's fix: selects its step, opens the step's sheet (unless it is open on that step already, so typed text stays) and names the field to focus.</summary>
    /// <param name="finding">The finding; one the app has no fix for does nothing.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the sheet is open on the step.</returns>
    internal async Task FixFinding(WorkflowFindingViewModel finding, CancellationToken cancellationToken)
    {
        if (finding.FixField is not { } fieldName || finding.Node is not { } node || Runs.Graph is not { } graph)
        {
            return;
        }

        var openOnIt = IsSettingsOpen && string.Equals(graph.SelectedId, node, StringComparison.Ordinal);
        if (!graph.SelectNode(node))
        {
            return;   // the step has no card on this graph (no served position): there is no sheet to open on it
        }

        if (!openOnIt)
        {
            await OpenSettings(cancellationToken).ConfigureAwait(true);
        }

        if (IsSettingsOpen)
        {
            (FocusFieldName, IsFieldPickerAsked) = (fieldName, true);
        }
    }

    private void WatchFindings()
    {
        Runs.PropertyChanged += OnRunsForFindings;
        Findings.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(WorkflowFindingsViewModel.All), StringComparison.Ordinal))
            {
                Findings.Mark(Runs.Graph);
                PickStepFindings();
            }
        };
        PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(IsSettingsOpen), StringComparison.Ordinal))
            {
                OnChanged(nameof(ShowsStepFindingsUnderGraph));
                if (!IsSettingsOpen)
                {
                    FocusFieldName = null;
                    IsFieldPickerAsked = false;
                }
            }
        };
    }

    private void OnRunsForFindings(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(RunsViewModel.Shown), StringComparison.Ordinal))
        {
            AskFindings();
        }
        else if (string.Equals(e.PropertyName, nameof(RunsViewModel.Graph), StringComparison.Ordinal) && !ReferenceEquals(_findingsGraph, Runs.Graph))
        {
            _findingsGraph?.PropertyChanged -= OnGraphForFindings;
            _findingsGraph = Runs.Graph;
            _findingsGraph?.PropertyChanged += OnGraphForFindings;
            Findings.Mark(Runs.Graph);   // a graph read again is built without marks
            PickStepFindings();
        }
    }

    private void OnGraphForFindings(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(WorkflowGraphViewModel.SelectedId), StringComparison.Ordinal))
        {
            PickStepFindings();
        }
    }

    private void PickStepFindings() => StepFindings = Findings.For(Runs.Graph?.SelectedId);

    private void AskFindings()
    {
        if (Runs.Shown is not { } shown || _url is not { } url)
        {
            _findingsOf = null;
            _findingReads++;   // no workflow: an answer on its way is not wanted
            Findings.Clear();
            return;
        }

        var of = (shown.Slug, shown.LatestRevision, shown.SpecHash);
        if (_findingsOf == of)
        {
            return;
        }

        _findingsOf = of;
        FindingsRead = ReadFindings(url, shown.Slug);
    }

    private async Task ReadFindings(Uri url, string slug)
    {
        var read = ++_findingReads;
        var answer = await Api.ValidateWorkflow(url, slug, CancellationToken.None).ConfigureAwait(true);
        if (read != _findingReads || !string.Equals(Runs.Slug, slug, StringComparison.Ordinal))
        {
            return;   // a newer revision was asked about since, or another workflow opened
        }

        if (answer.IsSuccess)
        {
            Findings.Show(answer.Value);
        }
        else
        {
            Findings.Clear();   // a failed check shows no findings rather than an older revision's
        }
    }
}
