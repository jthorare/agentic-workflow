namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>How the rows inside a group are ordered (the Sort menu, key O).</summary>
internal enum ProjectSort
{
    /// <summary>The served order: newest activity first, then name.</summary>
    Activity = 1,

    /// <summary>The soonest target first; a project without one goes last.</summary>
    Target = 2,

    /// <summary>A to Z.</summary>
    Name = 3,
}
