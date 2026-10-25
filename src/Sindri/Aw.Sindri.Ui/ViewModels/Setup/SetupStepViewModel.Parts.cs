namespace Aw.Sindri.Ui.ViewModels.Setup;

internal sealed partial class SetupStepViewModel
{
    /// <summary>The web address in the line under the title or in the detail; null when the words carry none.</summary>
    public Uri? LinkUrl => SetupLinks.UrlIn(Sub) ?? SetupLinks.UrlIn(DetailText);

    /// <summary>True when the row offers a link.</summary>
    public bool HasLink => LinkUrl is not null;

    /// <summary>The link button's accessible name, with the step so each link is told apart.</summary>
    public string OpenLinkName => Labels.SetupOpenLinkFor(Title);

#pragma warning disable CA1822, MA0041 // why: the view binds it as an instance property of the view model
    /// <summary>The link button's word.</summary>
    public string OpenLinkLabel => Labels.SetupOpenLink;
#pragma warning restore CA1822, MA0041

    /// <summary>True when the step was skipped.</summary>
    internal bool IsSkipped => string.Equals(Step.State, "skipped", StringComparison.Ordinal);

    /// <summary>True when the step is done or skipped: setup is finished when every step is.</summary>
    internal bool IsFinished => IsDone || IsSkipped;
}
