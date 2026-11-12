using System.ComponentModel;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): what POST /sindri/workflows/validate found on the open workflow's newest revision, and the header's one primary button (spec 4.5, 4.7).</summary>
internal sealed partial class WorkflowsPageViewModel
{
    private static readonly HashSet<string> _triggerTypes = new(["manual", "schedule", "connection_event"], StringComparer.Ordinal);

    private string _checked = string.Empty;   // "slug@revision" of the newest check started: a revision is checked once

    /// <summary>The findings of the last check, errors and warnings.</summary>
    public IReadOnlyList<WorkflowFindingView> Findings { get; private set => Set(ref field, value); } = [];

    /// <summary>"2 things to fix"; empty when no finding is an error.</summary>
    public string FindingsText => ErrorCount > 0 ? EditorStates.ThingsToFix(ErrorCount) : string.Empty;

    /// <summary>True when the findings chip shows.</summary>
    public bool HasFindings => ErrorCount > 0;

    /// <summary>True while the checks are not built yet (SND-GRAPH-119): the page shows no chip and no error.</summary>
    public bool ValidationNotBuilt { get; private set => Set(ref field, value); }

    /// <summary>The header's primary button: the one next step ("Add a trigger", "Fix 2 things", "Test run", "Activate").</summary>
    public string PrimaryText => EditorStates.Primary(Snapshot());

    /// <summary>The check started when the newest revision changed.</summary>
    internal Task Validating { get; private set; } = Task.CompletedTask;

    private int ErrorCount => Findings.Count(f => string.Equals(f.Severity, "error", StringComparison.Ordinal));

    private void WatchFindings()
    {
        Runs.PropertyChanged += OnRunsChangedForFindings;
        Editor.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(OpsEditor.NeedsReload), StringComparison.Ordinal))
            {
                OnChanged(nameof(PrimaryText));
            }
        };
    }

    // A new newest revision is checked once. The check is a read: nothing changes on the server.
    private void OnRunsChangedForFindings(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(RunsViewModel.Shown), StringComparison.Ordinal) && !string.Equals(e.PropertyName, nameof(RunsViewModel.StateChipText), StringComparison.Ordinal))
        {
            return;
        }

        OnChanged(nameof(PrimaryText));
        if (Runs.Shown is not { } shown || _url is not { } url)
        {
            return;
        }

        var key = shown.Slug + "@" + shown.LatestRevision.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (string.Equals(key, _checked, StringComparison.Ordinal))
        {
            return;
        }

        if (!_checked.StartsWith(shown.Slug + "@", StringComparison.Ordinal))
        {
            ShowFindings([], notBuilt: false);   // another workflow: none of the old one's findings stay
        }

        _checked = key;
        Validating = Validate(url, shown.Slug, shown.LatestRevision, key);
    }

    private async Task Validate(Uri url, string slug, int revision, string key)
    {
        var answer = await Api.ValidateWorkflow(url, slug, revision, CancellationToken.None).ConfigureAwait(true);
        if (!string.Equals(key, _checked, StringComparison.Ordinal))
        {
            return;   // a newer revision or another workflow is being checked
        }

        if (answer.IsSuccess)
        {
            ShowFindings(answer.Value.Findings, notBuilt: false);
            return;
        }

        // Not built yet is a quiet state. A check that could not be read shows no chip either: the page never invents a finding.
        ShowFindings([], string.Equals(answer.Error.Code, GraphNotBuilt, StringComparison.Ordinal));
    }

    private void ShowFindings(IReadOnlyList<WorkflowFindingView> findings, bool notBuilt)
    {
        (Findings, ValidationNotBuilt) = (findings, notBuilt);
        OnChanged(nameof(FindingsText));
        OnChanged(nameof(HasFindings));
        OnChanged(nameof(PrimaryText));
    }

    private EditorSnapshot Snapshot()
    {
        var nodes = Runs.Graph?.Nodes ?? [];
        var triggers = nodes.Count(n => IsTrigger(n.Type));
        return new EditorSnapshot(
            Conflict: Editor.NeedsReload,
            HasTrigger: triggers > 0,
            HasAction: nodes.Count > triggers,
            Findings: ErrorCount,
            Tested: Runs.IsTested);
    }

    private bool IsTrigger(string type) =>
        Editor.Groups.SelectMany(g => g.Blocks).FirstOrDefault(b => string.Equals(b.Type, type, StringComparison.Ordinal)) is { } block ? block.IsTrigger : _triggerTypes.Contains(type);
}
