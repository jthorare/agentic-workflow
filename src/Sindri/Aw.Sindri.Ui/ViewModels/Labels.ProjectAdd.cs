namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Add project sheet (projects page spec §3.2). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The sheet's heading, its accessible name and its title.</summary>
    internal const string ProjectAddHeading = "Add project";

    /// <summary>The From Linear tab.</summary>
    internal const string ProjectAddFromLinear = "From Linear";

    /// <summary>The By hand tab.</summary>
    internal const string ProjectAddByHand = "By hand";

    /// <summary>The From Linear tab's search box: its watermark and accessible name.</summary>
    internal const string ProjectAddSearch = "Search Linear projects";

    /// <summary>The From Linear tab while the list is being read.</summary>
    internal const string ProjectAddLinearLoading = "Loading…";

    /// <summary>The From Linear tab when the list is empty.</summary>
    internal const string ProjectAddLinearEmpty = "No other Linear projects in your teams.";

    /// <summary>The From Linear tab when the search matches no row.</summary>
    internal const string ProjectAddLinearNoMatch = "No project matches that search.";

    /// <summary>Under the From Linear list.</summary>
    internal const string ProjectAddLinearFootnote = "Only projects already synced; nothing is fetched from Linear while you search.";

    /// <summary>A row's Attach button.</summary>
    internal const string ProjectAddAttach = "Attach";

    /// <summary>A row's Attach button while its write is in flight.</summary>
    internal const string ProjectAddAttaching = "Attaching…";

    /// <summary>The notes field.</summary>
    internal const string ProjectAddNotesField = "Notes";

    /// <summary>The line under Save when the target date does not read as a date.</summary>
    internal const string ProjectAddBadTarget = "Target date must look like 2026-01-31.";

    /// <summary>A row's Attach button's accessible name.</summary>
    internal static string ProjectAddAttachName(string name) => "Attach " + name;

    /// <summary>"Lead: Ana Ruiz · In progress · 6 open issues"; the lead and the state are left out when null.</summary>
    internal static string ProjectLinearDetail(string? lead, string? state, int openIssues)
    {
        var parts = new List<string>(3);
        if (lead is not null)
        {
            parts.Add(ProjectLead(you: false, lead));
        }

        if (state is not null)
        {
            parts.Add(ProjectStateWord(state));
        }

        parts.Add(ProjectOpenIssues(openIssues));
        return string.Join(" · ", parts);
    }

    /// <summary>The line in place of the From Linear rows when the list could not be read.</summary>
    internal static string ProjectAddLinearFailed(Kernel.Error error) => "Couldn't read the Linear projects. " + InstanceRefused(error);

    /// <summary>The announcement of an added project.</summary>
    internal static string ProjectAdded(string name) => $"{name} added";

    /// <summary>The line under the save button when the add was refused.</summary>
    internal static string ProjectAddFailed(Kernel.Error error) => "Couldn't add the project. " + InstanceRefused(error);
}
