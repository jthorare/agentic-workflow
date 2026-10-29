using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Projects page in the shell (<see cref="ShellViewModel"/>, continued; Projects page spec §7): the tab after Home. It reads while it is the page shown.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Projects page's list.</summary>
    public ProjectsPageViewModel Projects { get; private set; }

    /// <summary>True on Projects.</summary>
    public bool IsProjects => CurrentPage == PageId.Projects;

    [MemberNotNull(nameof(Projects))]
    private void InitProjects()
    {
        Projects = new ProjectsPageViewModel(_api, _clock);
        Health.Follow(Projects.Refresh);
        PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(IsProjects), StringComparison.Ordinal))
            {
                Projects.SetShown(IsProjects);
            }
        };
    }
}
