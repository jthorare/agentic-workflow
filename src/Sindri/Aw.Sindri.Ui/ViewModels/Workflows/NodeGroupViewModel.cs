using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One node of a run with each of its executions: one for a plain node, one per item for a for_each node (grouped by the served itemIndex).</summary>
internal sealed class NodeGroupViewModel : ObservableObject
{
    internal NodeGroupViewModel(string node, IReadOnlyList<NodeRunView> runs, Func<NodeRunViewModel, bool, CancellationToken, Task>? control = null, Func<string, CancellationToken, Task>? edit = null, string nodeType = "")
    {
        Node = node;
        IsForEach = runs.Count > 1 || runs.Any(r => r.ItemIndex > 0);
        HeaderText = IsForEach ? Labels.WfNodeItems(node, runs.Count) : node;
        Runs = [.. runs.OrderBy(r => r.ItemIndex).Select(r => new NodeRunViewModel(r, runs.Count, control, edit, nodeType))];
    }

    /// <summary>The node's id.</summary>
    public string Node { get; }

    /// <summary>The node's id, or "agent · 3 items" for a for_each node.</summary>
    public string HeaderText { get; }

    /// <summary>True when the node ran for more than one item.</summary>
    public bool IsForEach { get; }

    /// <summary>Its executions, by item.</summary>
    public IReadOnlyList<NodeRunViewModel> Runs { get; }
}
