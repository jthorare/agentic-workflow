namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>Whether a step's body has anything to show, and so whether a failed row opens (#3616).</summary>
internal sealed partial class SetupStepViewModel
{
    /// <summary>True when the open row would show something.</summary>
    public bool HasBody => IsFailed;
}
