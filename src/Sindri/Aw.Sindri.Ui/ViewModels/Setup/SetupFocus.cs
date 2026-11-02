namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>Where the view should put keyboard focus in a setup row's form. The view model asks; it holds no control.</summary>
internal enum SetupFocus
{
    /// <summary>The repo form's folder field.</summary>
    RepoPath,

    /// <summary>The heading of the setup step the form is in (its header toggle), after the form's control left the tree.</summary>
    StepHeading,
}
