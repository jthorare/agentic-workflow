namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The step sheet's Advanced group (#3684): the only place the selected step's id shows.</summary>
internal sealed partial class OpsEditor
{
    /// <summary>The selected step's id line for the sheet's Advanced group; empty when none is selected or a new step is being filled in.</summary>
    public string NodeIdLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The Advanced group's name.</summary>
    public string AdvancedText => Labels.WfFieldAdvanced;
}
