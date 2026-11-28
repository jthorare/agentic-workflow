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
        var names = left.Keys.Concat(right.Keys.Where(k => !left.ContainsKey(k))).ToList();   // the newer run's steps in its order, then the ones only the older one walked
        Steps = [.. names.Select(n => new CompareStepViewModel(n, kinds.GetValueOrDefault(n, string.Empty), Cell(left, n), Cell(right, n)))];
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

    /// <summary>The steps, in the order the newer run walked them, then the ones only the older one did.</summary>
    public IReadOnlyList<CompareStepViewModel> Steps { get; } = [];

    /// <summary>"differs in 3 of 3 steps" or "the two runs match".</summary>
    public string SummaryText { get; } = string.Empty;

    /// <summary>"Run A is still running; its steps so far are shown"; empty when neither is.</summary>
    public string RunningNote { get; } = string.Empty;

    /// <summary>True when a run is still running.</summary>
    public bool HasRunningNote => RunningNote.Length > 0;

    private static Dictionary<string, NodeRunView> First(WorkflowRunView run)
    {
        var steps = new Dictionary<string, NodeRunView>(StringComparer.Ordinal);
        foreach (var node in run.Nodes)
        {
            _ = steps.TryAdd(node.Node, node);
        }

        return steps;
    }

    private static CompareCellViewModel Cell(Dictionary<string, NodeRunView> steps, string name) => steps.TryGetValue(name, out var run) ? new CompareCellViewModel(run) : new CompareCellViewModel();
}
