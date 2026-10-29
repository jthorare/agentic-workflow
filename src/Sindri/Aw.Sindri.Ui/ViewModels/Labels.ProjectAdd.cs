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

    /// <summary>The From Linear tab's body until its list ships.</summary>
    internal const string ProjectAddFromLinearSoon = "Coming soon";

    /// <summary>The notes field.</summary>
    internal const string ProjectAddNotesField = "Notes";

    /// <summary>The line under Save when the target date does not read as a date.</summary>
    internal const string ProjectAddBadTarget = "Target date must look like 2026-01-31.";

    /// <summary>The announcement of an added project.</summary>
    internal static string ProjectAdded(string name) => $"{name} added";

    /// <summary>The line under the save button when the add was refused.</summary>
    internal static string ProjectAddFailed(Kernel.Error error) => "Couldn't add the project. " + InstanceRefused(error);
}
