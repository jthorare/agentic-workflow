namespace Aw.Sindri.Ui.ViewModels.Settings;

/// <summary>Which Settings sections a typed filter keeps (#3362): a section stays when the filter is empty or every word typed is part of the text the section shows or of one of its synonyms (zoom, font, animation).</summary>
internal static class SettingsFilter
{
    // What a person might type to find each section: its headings and the settings in it.
    private static readonly string[][] _words =
    [
        ["setup", "checklist", "step", "profile", "folder", "repo", "repository", "onboard"],
        ["model", "label", "product surface", "surface", "choice", "pick", "agent"],
        ["sandbox", "network", "isolation", "files", "egress", "access"],
        ["library", "vault", "docs", "reading", "roots", "folder"],
        ["connections", "linear", "obsidian", "todo", "note", "token", "api key"],
        ["this app", "appearance", "text size", "text", "size", "font", "scale", "zoom", "reduce motion", "motion", "animation"],
    ];

    /// <summary>Whether the section stays visible under the filter.</summary>
    /// <param name="section">The section.</param>
    /// <param name="filter">What the person typed; empty keeps every section.</param>
    /// <returns>True when the section stays.</returns>
    /// <param name="shownText">The text the section shows now (its headings, row labels, buttons and descriptions), read from the page; empty when it has not been read.</param>
    internal static bool Matches(SettingsSection section, string filter, string shownText = "")
    {
        var typed = filter.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var words = _words[(int)section];
        return typed.All(t => words.Any(w => w.Contains(t, StringComparison.OrdinalIgnoreCase)) || shownText.Contains(t, StringComparison.OrdinalIgnoreCase));
    }
}
