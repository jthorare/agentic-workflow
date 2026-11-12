namespace Aw.Sindri.Ui.ViewModels;

internal static partial class Labels
{
    /// <summary>The Settings filter box's placeholder.</summary>
    internal const string SettingsFilterPlaceholder = "Filter settings";

    /// <summary>The line under the Settings filter box when no setting matches.</summary>
    /// <param name="filter">What was typed.</param>
    /// <returns>The words.</returns>
    internal static string SettingsNoMatch(string filter) => "No settings match “" + filter + "”.";
}
