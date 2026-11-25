using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The open workflow's validation findings (#3803), as POST /sindri/workflows/validate served them: the header's count, the findings of one step, and the marks on the graph's cards.
/// The app judges nothing: what the server sent is what shows.
/// </summary>
internal sealed class WorkflowFindingsViewModel : ObservableObject
{
    /// <summary>Every finding, in the order the server listed them.</summary>
    public IReadOnlyList<WorkflowFindingViewModel> All { get; private set { Set(ref field, value); OnChanged(nameof(HasFindings)); OnChanged(nameof(HeaderText)); } } = [];

    /// <summary>True when the server found something.</summary>
    public bool HasFindings => All.Count > 0;

    /// <summary>"1 thing to fix", "3 things to fix"; empty with none.</summary>
    public string HeaderText => Labels.WfFindingsHead(All.Count);

    /// <summary>The findings that name one step, in the server's order.</summary>
    /// <param name="node">The step's id, or null.</param>
    /// <returns>The findings, empty for null or a step with none.</returns>
    internal IReadOnlyList<WorkflowFindingViewModel> For(string? node) =>
        node is null ? [] : [.. All.Where(f => string.Equals(f.Node, node, StringComparison.Ordinal))];

    /// <summary>Shows what the server found.</summary>
    /// <param name="validated">The validate answer.</param>
    internal void Show(WorkflowValidated validated) => All = [.. validated.Findings.Select(f => new WorkflowFindingViewModel(f))];

    /// <summary>The findings about the whole workflow, not one step (they have no node), in the server's order. Not listed yet.</summary>
    public IReadOnlyList<WorkflowFindingViewModel> WholeWorkflow { get; private set; } = [];

    /// <summary>True when some finding is about the whole workflow.</summary>
    public bool HasWholeWorkflow => WholeWorkflow.Count > 0;

    /// <summary>The line that says the last check failed; empty when it did not. Not shown yet.</summary>
    public string FailedText { get; private set; } = string.Empty;

    /// <summary>True when the last check failed.</summary>
    public bool HasFailed => FailedText.Length > 0;

    /// <summary>Forgets the findings (another workflow opened, or none is).</summary>
    internal void Clear()
    {
        if (All.Count > 0)
        {
            All = [];
        }
    }

    /// <summary>Marks each card of a graph with the findings that name it, and clears the marks of the rest.</summary>
    /// <param name="graph">The graph shown, or null.</param>
    internal void Mark(WorkflowGraphViewModel? graph) => graph?.MarkFindings(All);
}
