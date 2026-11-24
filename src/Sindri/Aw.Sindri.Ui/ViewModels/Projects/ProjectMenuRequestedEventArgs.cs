namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>A key asked the view to open one of the bar's menus.</summary>
/// <param name="menu">The menu.</param>
internal sealed class ProjectMenuRequestedEventArgs(ProjectMenu menu) : EventArgs
{
    /// <summary>The menu to open.</summary>
    public ProjectMenu Menu { get; } = menu;
}
