using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>The runs sidebar (<see cref="RunPageViewModel"/>, continued): the open runs of the same workflow, narrowed by the search box.</summary>
internal sealed partial class RunPageViewModel
{
    private IReadOnlyList<RunListItemViewModel> _allRuns = [];

    /// <summary>The runs the search leaves, in the served order.</summary>
    public IReadOnlyList<RunListItemViewModel> Runs { get; private set => Set(ref field, value); } = [];

    /// <summary>What is typed in the search box.</summary>
    public string RunsFilter
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            field = value;
            OnChanged(nameof(RunsFilter));
            Narrow();
        }
    } = string.Empty;

    /// <summary>Opens a run of the list: its canvas, or Needs you for a run waiting on you (owner answer Q3).</summary>
    /// <param name="run">The card.</param>
    internal void OpenRun(RunListItemViewModel run)
    {
        ArgumentNullException.ThrowIfNull(run);
        Go(run.Link);
    }

    private void ShowRuns(IReadOnlyList<InstanceSummary> rows, string current)
    {
        var now = clock.GetUtcNow();
        _allRuns = [.. rows.Select(r => new RunListItemViewModel(r, now) { IsCurrent = string.Equals(r.Address, current, StringComparison.Ordinal) })];
        Narrow();
    }

    private void Narrow() =>
        Runs = RunsFilter.Trim().Length == 0
            ? _allRuns
            : [.. _allRuns.Where(r => r.Title.Contains(RunsFilter.Trim(), StringComparison.OrdinalIgnoreCase) || r.Address.Contains(RunsFilter.Trim(), StringComparison.OrdinalIgnoreCase))];

    private void ClearRuns()
    {
        _allRuns = [];
        Runs = [];
    }
}
