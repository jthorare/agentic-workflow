namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the New workflow form's fixed words.</summary>
internal sealed partial class WorkflowsPageViewModel
{
#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The name field's label.</summary>
    public string NewNameLabel => Labels.WfNewName;

    /// <summary>The short id field's label.</summary>
    public string ShortIdLabel => Labels.WfNewShortId;

    /// <summary>The template picker's label.</summary>
    public string TemplateLabel => Labels.WfNewTemplate;

    /// <summary>The Create button's words.</summary>
    public string CreateText => Labels.WfCreate;

    /// <summary>The Cancel button's words.</summary>
    public string CancelCreateText => Labels.WfCreateCancel;
#pragma warning restore CA1822, MA0041
}
