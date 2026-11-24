using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// One node of a run with each of its executions: one for a plain node, one per item for a for_each node (grouped by the served itemIndex). The server counts items from 1;
/// index 0 is the node's own row, which a collect has beside its item rows (its result), so the item count is the rows above 0, not every row (#4082).
/// </summary>
internal sealed class NodeGroupViewModel : ObservableObject
{
    internal NodeGroupViewModel(string node, IReadOnlyList<NodeRunView> runs, Func<NodeRunViewModel, bool, CancellationToken, Task>? control = null, Func<string, CancellationToken, Task>? edit = null, string nodeType = "")
    {
        Node = node;
        var items = runs.Count(static r => r.ItemIndex > 0);
        IsForEach = items > 0;
        HeaderText = IsForEach ? Labels.WfNodeItems(node, items) : node;
        Runs = [.. runs.OrderBy(r => r.ItemIndex).Select(r => new NodeRunViewModel(r, items, control, edit, nodeType))];
    }

    /// <summary>The node's id.</summary>
    public string Node { get; }

    /// <summary>The node's id, or "agent · 3 items" for a for_each node.</summary>
    public string HeaderText { get; }

    /// <summary>True when the node ran per item (a for_each body step or a collect).</summary>
    public bool IsForEach { get; }

    /// <summary>Its executions, by item.</summary>
    public IReadOnlyList<NodeRunViewModel> Runs { get; }
}
