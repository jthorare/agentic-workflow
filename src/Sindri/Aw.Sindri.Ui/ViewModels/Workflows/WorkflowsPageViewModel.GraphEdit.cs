namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the graph edits of the open workflow.</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>The graph edits (Add step, Remove, Connect) of the open workflow.</summary>
    public GraphEditViewModel GraphEdit { get; }

    /// <summary>True while the editor sheet is open on a step that does not exist yet (Add step picked a kind; Save sends the add_node).</summary>
    public bool IsNewStep { get { _ = IsEditing; throw new NotSupportedException(); } }
}
