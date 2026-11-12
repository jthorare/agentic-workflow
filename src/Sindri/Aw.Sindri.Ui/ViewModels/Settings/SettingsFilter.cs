namespace Aw.Sindri.Ui.ViewModels.Settings;

/// <summary>Which Settings sections a typed filter keeps (#3362): a section stays when the filter is empty or the text is part of one of the section's words.</summary>
internal static class SettingsFilter
{
    /// <summary>Whether the section stays visible under the filter.</summary>
    /// <param name="section">The section.</param>
    /// <param name="filter">What the person typed; empty keeps every section.</param>
    /// <returns>True when the section stays.</returns>
    internal static bool Matches(SettingsSection section, string filter) => string.IsNullOrEmpty(filter) || section == SettingsSection.ThisApp;   // red stub (#3362): the fix reads each section's words
}
