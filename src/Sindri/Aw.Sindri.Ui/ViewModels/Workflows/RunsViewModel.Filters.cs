using System.Text.Json;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One workflow's runs (<see cref="RunsViewModel"/>, continued): the status and date filters, the count, and the ticks that make Compare (#3683).</summary>
internal sealed partial class RunsViewModel
{
    /// <summary>"23 of 500 runs" with a filter on, "500 runs" without.</summary>
    public string CountText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when a status or date filter is on.</summary>
    public bool HasFilter => !string.IsNullOrEmpty(StatusFilter) || !string.IsNullOrEmpty(DateFilter);

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
    public string CompareHint => SelectedCount switch { 1 => "Tick one more run to compare", >= 3 => "Compare takes two runs: untick one", _ => string.Empty };

    /// <summary>True when there is a hint.</summary>
    public bool HasCompareHint => CompareHint.Length > 0;

    /// <summary>The block type of each step of the revision shown ("sum" to "code"), for the compare view's step kinds.</summary>
    internal IReadOnlyDictionary<string, string> NodeKinds
    {
        get
        {
            var kinds = new Dictionary<string, string>(StringComparer.Ordinal);
            if (Shown?.Spec is { ValueKind: JsonValueKind.Object } spec && spec.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array)
            {
                foreach (var node in nodes.EnumerateArray())
                {
                    if (node.TryGetProperty("id", out var id) && node.TryGetProperty("type", out var type) && id.GetString() is { } key && type.GetString() is { } kind)
                    {
                        kinds[key] = kind;
                    }
                }
            }

            return kinds;
        }
    }

    /// <summary>The ticked runs, newest first, whether or not a filter hides them.</summary>
    internal IReadOnlyList<RunRowViewModel> Ticked => [.. _all.Where(r => r.IsSelected)];

    /// <summary>"Compare 2".</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string CompareText => Labels.WfCompare(2);
#pragma warning restore CA1822, MA0041

    /// <summary>The status filter's choices.</summary>
    public IReadOnlyList<RunFilterChoice> StatusChoices { get; } = [new("", "All statuses"), new("failed", "Failed"), new("succeeded", "Succeeded"), new("running", "Running"), new("cancelled", "Cancelled")];

    /// <summary>The date filter's choices.</summary>
    public IReadOnlyList<RunFilterChoice> DateChoices { get; } = [new("", "Any time"), new("24h", "Last 24 hours"), new("7d", "Last 7 days"), new("30d", "Last 30 days")];

    /// <summary>The picked status choice; picking one filters.</summary>
    public RunFilterChoice SelectedStatus { get => StatusChoices.First(c => string.Equals(c.Key, StatusFilter, StringComparison.Ordinal)); set => SetStatusFilter(value?.Key); }

    /// <summary>The picked date choice; picking one filters.</summary>
    public RunFilterChoice SelectedDate { get => DateChoices.First(c => string.Equals(c.Key, DateFilter, StringComparison.Ordinal)); set => SetDateFilter(value?.Key); }

    /// <summary>The status filter: "failed", "succeeded", "running", "cancelled", or empty for every status.</summary>
    public string StatusFilter { get; private set; } = string.Empty;

    /// <summary>The date filter: "24h", "7d", "30d", or empty for every date.</summary>
    public string DateFilter { get; private set; } = string.Empty;

    /// <summary>Filters by status ("failed", "succeeded", "running", "cancelled"); null or empty shows every status.</summary>
    /// <param name="status">The status.</param>
    internal void SetStatusFilter(string? status)
    {
        StatusFilter = status ?? string.Empty;
        Apply();
    }

    /// <summary>Filters by date ("24h", "7d", "30d"); null or empty shows every date.</summary>
    /// <param name="range">The range.</param>
    internal void SetDateFilter(string? range)
    {
        DateFilter = range ?? string.Empty;
        Apply();
    }

    /// <summary>Clears both filters.</summary>
    internal void ClearFilters()
    {
        (StatusFilter, DateFilter) = (string.Empty, string.Empty);
        Apply();
    }

    // The rows the filters keep, newest first as served; the count says how many of all.
    private void Apply()
    {
        var since = DateFilter switch { "24h" => TimeSpan.FromHours(24), "7d" => TimeSpan.FromDays(7), "30d" => TimeSpan.FromDays(30), _ => TimeSpan.Zero };
        var now = clock.GetUtcNow();
        var kept = _all.Where(r => (StatusFilter.Length == 0 || string.Equals(r.Status, StatusFilter, StringComparison.Ordinal)) && (since == TimeSpan.Zero || now - r.StartedAt <= since)).ToList();
        var starting = _starting.Select(id => Rows.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal) && string.Equals(r.Status, "starting", StringComparison.Ordinal)) ?? Placeholder(id));   // a Starting placeholder is not a served run: it stays above the served rows, whatever the filters say
        Patch.To(Rows, [.. starting, .. kept]);
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
        OnChanged(nameof(CompareHint));
        OnChanged(nameof(HasCompareHint));
    }
}
