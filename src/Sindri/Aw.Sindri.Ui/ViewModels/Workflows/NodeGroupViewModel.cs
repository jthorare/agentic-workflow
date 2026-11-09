using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One node of a run with each of its executions: one for a plain node, one per item for a for_each node. Red stub.</summary>
internal sealed class NodeGroupViewModel : ObservableObject
{
    internal NodeGroupViewModel(string node, IReadOnlyList<NodeRunView> runs)
    {
    }

    public string Node => throw new NotSupportedException();

    public string HeaderText => throw new NotSupportedException();

    public bool IsForEach => throw new NotSupportedException();

    public IReadOnlyList<NodeRunViewModel> Runs => throw new NotSupportedException();
}
