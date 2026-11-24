namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>How the rows are grouped (the Group menu, key B).</summary>
internal enum ProjectGrouping
{
    /// <summary>By Sindri's health level: Off track, At risk, CI not checked, On track, then No open PRs.</summary>
    Health = 1,

    /// <summary>By the project's state: In progress, Planned, Paused, Backlog, then any other state.</summary>
    State = 2,

    /// <summary>By the repo of each open PR that is not a draft; a project with two repos shows under both.</summary>
    Repo = 3,

    /// <summary>One list, no group heads.</summary>
    None = 4,
}
