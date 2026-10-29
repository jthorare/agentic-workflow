using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Projects page in the shell (<see cref="ShellViewModel"/>, continued; Projects page spec §7): the tab after Home. It reads while it is the page shown.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Projects page's list.</summary>
    public ProjectsPageViewModel Projects { get; private set; }

    /// <summary>The project card's sheet.</summary>
    public ProjectSheetViewModel ProjectSheet { get; private set; }

    /// <summary>The Add project sheet.</summary>
    public ProjectAddViewModel ProjectAdd { get; private set; }

    /// <summary>True when the sheet is the Add project sheet.</summary>
    public bool IsProjectAddSheet => SheetLink?.Kind == DeepLinkKind.ProjectAdd;

    /// <summary>True when the sheet's title is a name, not an id (a project's, or "Add project"): it is not set in mono.</summary>
    public bool SheetTitleIsMono => !IsProjectSheet && !IsProjectAddSheet;

    /// <summary>True when the sheet's subject is a project.</summary>
    public bool IsProjectSheet => SheetLink?.Kind == DeepLinkKind.Project;

    /// <summary>The project whose card opened the sheet (its id), where Esc returns focus; null when it was opened some other way.</summary>
    internal string? OpenerProject { get; private set; }

    /// <summary>True when the Add project button opened the sheet, so Esc returns focus to it.</summary>
    internal bool OpenerAdd { get; private set; }

    /// <summary>True on Projects.</summary>
    public bool IsProjects => CurrentPage == PageId.Projects;

    [MemberNotNull(nameof(Projects), nameof(ProjectSheet), nameof(ProjectAdd))]
    private void InitProjects(ILauncher launcher)
    {
        Projects = new ProjectsPageViewModel(_api, _clock);
        ProjectSheet = new ProjectSheetViewModel(_api, launcher, _clock);
        ProjectAdd = new ProjectAddViewModel(_api, Health.ApiUrl);
        Health.Follow(Projects.Refresh);
        PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(IsProjects), StringComparison.Ordinal))
            {
                Projects.SetShown(IsProjects);
            }
        };
        Health.Follow((url, token) => IsProjectSheet && IsSheetOpen ? ProjectSheet.Refresh(url, token) : Task.CompletedTask);
        Projects.OpenRequested += (_, e) =>
        {
            OpenSheet(e.Link);
            OpenerProject = Uri.UnescapeDataString(e.Link.Subject ?? string.Empty);   // Esc comes back to this card, after the sheet's own opening cleared it
        };
        ProjectSheet.Done += OnProjectDone;
        WireProjectAdd();
        ProjectSheet.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(ProjectSheetViewModel.Heading), StringComparison.Ordinal))
            {
                Changed(nameof(SheetTitle), nameof(SheetName), nameof(EdgeTabName), nameof(EdgeTabText));
            }
            else if (string.Equals(e.PropertyName, nameof(ProjectSheetViewModel.Announcement), StringComparison.Ordinal) && ProjectSheet.Announcement.Length > 0)
            {
                _line.Say(ProjectSheet.Announcement);
            }
        };
    }

    // The Add project sheet: the page's button opens it, Cancel closes it, and an added project closes it with the sentence said.
    private void WireProjectAdd()
    {
        Projects.AddRequested += (_, _) =>
        {
            OpenSheet(DeepLink.ProjectAdd);
            OpenerAdd = true;   // Esc comes back to the button, after the sheet's own opening cleared it
        };
        ProjectAdd.Cancelled += (_, _) => CloseSheet();
        ProjectAdd.Added += (sender, e) =>
        {
            OnProjectDone(sender, e);
            _line.Say(e.Said);
        };
    }

    // The project part of opening the sheet (OnSheetOpened): the sheet names its project at once and reads it.
    private void OnProjectSheetOpened(DeepLink link)
    {
        (OpenerProject, OpenerAdd) = (null, false);
        Changed(nameof(IsProjectSheet), nameof(IsProjectAddSheet), nameof(SheetTitleIsMono));
        if (link.Kind == DeepLinkKind.ProjectAdd)
        {
            ProjectAdd.Reset();
            return;
        }

        if (link.Kind != DeepLinkKind.Project || link.Subject is not { } subject)
        {
            return;
        }

        ProjectSheet.Show(subject);
        if (Health.ApiUrl() is { } url)
        {
            _ = ProjectSheet.Open(url, subject, CancellationToken.None);
        }
    }

    // The project part of reopening the sheet (ReopenSheet): the card may have changed while the sheet was closed.
    private void OnProjectSheetReopened()
    {
        if (IsProjectSheet && Health.ApiUrl() is { } url)
        {
            _ = ProjectSheet.Refresh(url, CancellationToken.None);
        }
    }

    // A write took the card off the page (or back): the sheet closes without an edge tab, since its card is gone, and the page reads again and moves focus.
    private void OnProjectDone(object? sender, ProjectDoneEventArgs e)
    {
        CloseSheet();
        SheetLink = null;
        Changed(nameof(SheetLink), nameof(SheetSubject), nameof(HasEdgeTab), nameof(IsEdgeTabShown), nameof(IsProjectSheet), nameof(IsProjectAddSheet), nameof(SheetTitleIsMono), nameof(SheetTitle), nameof(SheetName), nameof(EdgeTabName), nameof(EdgeTabText));
        _ = Projects.Settled(e, CancellationToken.None);
    }
}
