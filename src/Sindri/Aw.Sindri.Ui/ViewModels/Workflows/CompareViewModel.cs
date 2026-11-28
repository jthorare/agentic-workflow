using System.Globalization;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>Two runs compared step by step (frame wf-compare).</summary>
internal sealed class CompareViewModel
{
    internal CompareViewModel()
    {
        (A, B) = (CompareRunHeadViewModel.None, CompareRunHeadViewModel.None);   // nothing compared yet: empty heads
    }

    internal CompareViewModel(WorkflowRunView a, WorkflowRunView b, IReadOnlyDictionary<string, string> kinds, TimeProvider clock)
    {
        (A, B) = (new CompareRunHeadViewModel("A", a, clock), new CompareRunHeadViewModel("B", b, clock));
        var (left, right) = (First(a), First(b));
        var rows = left.Keys.Concat(right.Keys.Where(k => !left.ContainsKey(k))).ToList();   // the newer run's rows in its order, then the ones only the older one walked: one per step, and one per item of a for_each step
        Steps = [.. rows.Select(r => new CompareStepViewModel(RowName(r), kinds.GetValueOrDefault(r.Node, string.Empty), Cell(left, r), Cell(right, r)))];
        var differing = Steps.Count(s => s.Differs);
        SummaryText = differing == 0 ? "the two runs match" : string.Create(CultureInfo.InvariantCulture, $"differs in {differing} of {Steps.Count} {(Steps.Count == 1 ? "step" : "steps")}");
        var running = new[] { ("A", a), ("B", b) }.Where(r => RunStatus.IsLive(r.Item2.Status)).Select(r => r.Item1).ToList();
        RunningNote = running.Count switch
        {
            0 => string.Empty,
            1 => $"Run {running[0]} is still running; its steps so far are shown",
            _ => "Both runs are still running; their steps so far are shown",
        };
    }

    /// <summary>The newer run's head.</summary>
    public CompareRunHeadViewModel A { get; }

    /// <summary>The older run's head.</summary>
    public CompareRunHeadViewModel B { get; }

    /// <summary>The steps, in the order the newer run walked them, then the ones only the older one did; a for_each step has a row per item too.</summary>
    public IReadOnlyList<CompareStepViewModel> Steps { get; } = [];

    /// <summary>"differs in 3 of 3 steps" or "the two runs match".</summary>
    public string SummaryText { get; } = string.Empty;

    /// <summary>"Run A is still running; its steps so far are shown"; empty when neither is.</summary>
    public string RunningNote { get; } = string.Empty;

    /// <summary>True when a run is still running.</summary>
    public bool HasRunningNote => RunningNote.Length > 0;

    // A step's own row has item index 0; a for_each step also has one row per item, from 1.
    private static string RowName((string Node, int Item) row) => row.Item > 0 ? string.Create(CultureInfo.InvariantCulture, $"{row.Node} \u00b7 item {row.Item}") : row.Node;

    private static Dictionary<(string Node, int Item), NodeRunView> First(WorkflowRunView run)
    {
        var rows = new Dictionary<(string Node, int Item), NodeRunView>();
        foreach (var node in run.Nodes)
        {
            _ = rows.TryAdd((node.Node, node.ItemIndex), node);
        }

        return rows;
    }

    private static CompareCellViewModel Cell(Dictionary<(string Node, int Item), NodeRunView> rows, (string Node, int Item) row) => rows.TryGetValue(row, out var run) ? new CompareCellViewModel(run) : new CompareCellViewModel();
}
