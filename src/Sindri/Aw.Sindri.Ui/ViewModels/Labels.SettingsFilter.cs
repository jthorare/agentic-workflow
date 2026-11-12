namespace Aw.Sindri.Ui.ViewModels;

internal static partial class Labels
{
    /// <summary>The mark beside a setting that differs from its default.</summary>
    internal const string SettingChanged = "Changed";

    /// <summary>A changed setting's Reset button.</summary>
    internal const string SettingReset = "Reset";

    /// <summary>A palette entry for one setting.</summary>
    /// <param name="name">The setting's name.</param>
    /// <returns>"Settings: Text size".</returns>
    internal static string SettingsEntry(string name) => "Settings: " + name;

    /// <summary>The Settings filter box's placeholder.</summary>
    internal const string SettingsFilterPlaceholder = "Filter settings";

    /// <summary>The line under the Settings filter box when no setting matches.</summary>
    /// <param name="filter">What was typed.</param>
    /// <returns>The words.</returns>
    internal static string SettingsNoMatch(string filter) => "No settings match “" + filter + "”.";
}
