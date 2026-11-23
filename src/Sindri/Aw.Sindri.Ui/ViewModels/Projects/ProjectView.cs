namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>The Projects page's views, keys 1 to 4 (issue 3717): which projects the rows hold.</summary>
internal enum ProjectView
{
    /// <summary>Projects whose PRs wait on the owner, as the server flags them (issue 3797).</summary>
    NeedsMe = 1,

    /// <summary>Projects the owner leads in Linear or made by hand; attached ones are left out.</summary>
    MyWork = 2,

    /// <summary>Every open project, then the Finished and Hidden folds.</summary>
    All = 3,

    /// <summary>Not built yet: an empty state that says what it will hold.</summary>
    Triage = 4,
}
