using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the graph edits of the open workflow.</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>The graph edits (Add step, Remove, Connect) of the open workflow.</summary>
    public GraphEditViewModel GraphEdit { get; }

    /// <summary>True while the editor sheet is open on a step that does not exist yet (Add step picked a kind; Save sends the add_node).</summary>
    public bool IsNewStep => Editor.HasPendingStep;

    /// <summary>The name of the field the sheet focuses when it opens: the first required field, else the first field, else empty.</summary>
    public string FocusFieldName => (Editor.Fields.FirstOrDefault(static f => f.IsRequired) ?? Editor.Fields.FirstOrDefault())?.Name ?? string.Empty;

    // Add step picked a kind: the sheet opens on that kind's settings, nothing is sent.
    private void BeginNewStep(WorkflowBlock block, string id, string? after)
    {
        _edits++;   // an Edit read still on the way must not select a node over this form
        Editor.BeginNewStep(block, id, after);
        (IsEditing, IsSettingsOpen, GraphEdit.SheetOpen) = (true, true, true);
        OnChanged(nameof(IsNewStep));
        OnChanged(nameof(NoFieldsText));
        OnChanged(nameof(CanSave));
    }
}
