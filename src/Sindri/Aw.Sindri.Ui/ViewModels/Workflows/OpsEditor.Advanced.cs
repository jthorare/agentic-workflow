namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The step sheet's Advanced group (#3684): the only place the selected step's id shows, and the Code step's repo, branch, timeout and memory (#3682).</summary>
internal sealed partial class OpsEditor
{
    /// <summary>The selected step's id line for the sheet's Advanced group; empty when none is selected or a new step is being filled in.</summary>
    public string NodeIdLine { get; private set { Set(ref field, value); OnChanged(nameof(ShowsAdvanced)); } } = string.Empty;

    /// <summary>True when the selected step has settings in an Advanced group.</summary>
    public bool HasAdvancedFields => Fields.Any(f => f.InAdvanced);

    /// <summary>True when the sheet shows its Advanced group: the step has an id to show, or settings in the group (a new Code step has no id yet).</summary>
    public bool ShowsAdvanced => NodeIdLine.Length > 0 || HasAdvancedFields;

    /// <summary>True when the Advanced group is open: it shows the step's id and the settings in the group.</summary>
    public bool AdvancedOpen
    {
        get;
        set
        {
            Set(ref field, value);
            foreach (var f in Fields)
            {
                f.GroupOpen = value;
            }
        }
    }

    /// <summary>The Advanced group's name.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it (WorkflowEditorSheet.axaml, Editor.AdvancedText)
    public string AdvancedText => Labels.WfFieldAdvanced;
#pragma warning restore CA1822, MA0041
}
