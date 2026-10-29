namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): a project card's sheet (projects page spec §3.2). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The sheet before its first answer.</summary>
    internal const string ReadingProject = "Reading project…";

    /// <summary>The sheet for an id Sindri doesn't know.</summary>
    internal const string ProjectUnknown = "Sindri has no such project.";

    /// <summary>The edge tab's text while the project's name isn't read.</summary>
    internal const string EdgeTabProject = "Project";

    /// <summary>The note under a sheet whose refresh failed: the reason follows.</summary>
    internal static string ProjectSheetStale(Kernel.Error error) => "Couldn't refresh this project just now. " + InstanceRefused(error);

    /// <summary>The sheet when the answer can't be read.</summary>
    internal const string ProjectsUnreadable = "This project can't be read from this Sindri.";

    /// <summary>The issue count before it is final.</summary>
    internal const string ProjectIssuesCounting = "Counting open issues…";

    /// <summary>"Active 2h ago".</summary>
    internal static string ProjectActiveOn(TimeSpan age) => "Active " + ProjectActive(age);

    /// <summary>The Edit button.</summary>
    internal const string ProjectEdit = "Edit";

    /// <summary>The Set state button.</summary>
    internal const string ProjectSetState = "Set state";

    /// <summary>Gives the state back to Linear.</summary>
    internal const string ProjectUseLinearState = "Use Linear's state";

    /// <summary>The Remove button.</summary>
    internal const string ProjectRemove = "Remove";

    /// <summary>The Hide button.</summary>
    internal const string ProjectHide = "Hide";

    /// <summary>The Unhide button.</summary>
    internal const string ProjectUnhide = "Unhide";

    /// <summary>The save button at rest.</summary>
    internal const string ProjectSave = "Save";

    /// <summary>The save button while the write is in flight.</summary>
    internal const string ProjectSaving = "Saving…";

    /// <summary>Leaves the edit form without saving.</summary>
    internal const string ProjectCancelEdit = "Cancel";

    /// <summary>The name field.</summary>
    internal const string ProjectNameField = "Name";

    /// <summary>The target date field.</summary>
    internal const string ProjectTargetField = "Target date (yyyy-mm-dd)";

    /// <summary>The links field.</summary>
    internal const string ProjectLinksField = "Links (one https link per line)";

    /// <summary>The state picker.</summary>
    internal const string ProjectStateField = "State";

    /// <summary>The notes heading (read-only here).</summary>
    internal const string ProjectNotesHeading = "Notes";

    /// <summary>The links heading.</summary>
    internal const string ProjectLinksHeading = "Links";

    /// <summary>The repos heading.</summary>
    internal const string ProjectReposHeading = "Repos";

    /// <summary>The state line's mark for a state the owner set.</summary>
    internal const string ProjectSetByYou = "set by you";

    /// <summary>The keep button of the remove question.</summary>
    internal const string ProjectKeep = "Keep";

    /// <summary>The line when a write found the card changed since the sheet read it.</summary>
    internal const string ProjectChangedElsewhere = "Changed elsewhere since you opened it.";

    /// <summary>Reads the card again.</summary>
    internal const string ProjectReload = "Reload";

    /// <summary>The remove question.</summary>
    internal static string ProjectRemoveQuestion(string name) => $"Remove {name} from your page?";

    /// <summary>A repo that has no GitHub name to link.</summary>
    internal static string ProjectRepoNotSynced(string repo) => $"{repo}: PRs not synced for this repo";

    /// <summary>A repo's pull request list link.</summary>
    internal static string ProjectRepoPrs(string repo) => $"{repo} PRs ↗";

    /// <summary>The announcement of a finished save.</summary>
    internal static string ProjectSaved(string name) => $"{name} saved";

    /// <summary>The announcement of a removed attachment.</summary>
    internal static string ProjectRemoved(string name) => $"{name} removed";

    /// <summary>The announcement of a hidden project.</summary>
    internal static string ProjectHidden(string name) => $"{name} hidden";

    /// <summary>The announcement of a project brought back.</summary>
    internal static string ProjectUnhidden(string name) => $"{name} is back on your page";

    /// <summary>The line under the save button when the write was refused.</summary>
    internal static string ProjectWriteFailed(Kernel.Error error) => "Couldn't save. " + InstanceRefused(error);
}
