using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Projects page in the shell (<see cref="ShellViewModel"/>, continued; Projects page spec §7): the tab after Home. It has no view model of its own yet.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>True on Projects.</summary>
    public bool IsProjects => CurrentPage == PageId.Projects;
}
