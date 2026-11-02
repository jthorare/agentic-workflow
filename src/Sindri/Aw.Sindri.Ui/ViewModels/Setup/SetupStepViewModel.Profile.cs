namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>A setup step (<see cref="SetupStepViewModel"/>, continued): what the view asks about the profile approval.</summary>
internal sealed partial class SetupStepViewModel
{
    /// <summary>True when the profile approval has something to draw. A plain bool for the view: on every step but the profile one <see cref="Profile"/> is null, and a binding through a null leaves IsVisible at true (#1529).</summary>
    public bool ShowsProfile => Profile?.HasContent == true;
}
