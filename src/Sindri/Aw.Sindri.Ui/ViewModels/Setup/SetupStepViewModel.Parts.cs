namespace Aw.Sindri.Ui.ViewModels.Setup;

internal sealed partial class SetupStepViewModel
{
    /// <summary>The web address in the line under the title or in the detail; null when the words carry none.</summary>
    public Uri? LinkUrl => SetupLinks.UrlIn(Sub) ?? SetupLinks.UrlIn(DetailText);

    /// <summary>True when the row offers a link.</summary>
    public bool HasLink => LinkUrl is not null;

    /// <summary>True when the step was skipped.</summary>
    internal bool IsSkipped => string.Equals(Step.State, "skipped", StringComparison.Ordinal);

    /// <summary>True when the step is done or skipped: setup is finished when every step is.</summary>
    internal bool IsFinished => IsDone || IsSkipped;
}
