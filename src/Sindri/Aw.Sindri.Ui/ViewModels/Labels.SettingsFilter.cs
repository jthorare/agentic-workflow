namespace Aw.Sindri.Ui.ViewModels;

internal static partial class Labels
{
    /// <summary>The mark beside a setting that differs from its default.</summary>
    internal const string SettingChanged = "Changed";

    /// <summary>A changed setting's Reset button.</summary>
    internal const string SettingReset = "Reset";

    /// <summary>A changed setting's Reset button, named for the setting.</summary>
    /// <param name="name">The setting's name.</param>
    /// <returns>"Reset Text size".</returns>
    internal static string SettingResetName(string name) => "Reset " + name;

    /// <summary>A changed setting's accessible name.</summary>
    /// <param name="name">The setting's name.</param>
    /// <returns>"Text size, changed".</returns>
    internal static string SettingChangedName(string name) => name + ", changed";

    /// <summary>The Settings filter box's placeholder.</summary>
    internal static string SettingsFilterPlaceholder { get; } = "Filter settings  " + (OperatingSystem.IsMacOS() ? "⌘," : "Ctrl+,");

    /// <summary>The line under the Settings filter box when no setting matches.</summary>
    /// <param name="filter">What was typed.</param>
    /// <returns>The words.</returns>
    internal static string SettingsNoMatch(string filter) => "Nothing matches “" + filter + "”.";

    /// <summary>The second line of the no-match state: how to get the sections back.</summary>
    internal const string SettingsNoMatchHint = "Esc clears the filter.";
}
