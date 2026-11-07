using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the project sheet's Review settings (review page spec §7, Ph9). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The section's heading.</summary>
    internal const string ReviewSettingsHeading = "Review settings";

    /// <summary>"Settings entry: sindri".</summary>
    internal static string ReviewSettingsEntryLine(string name) => "Settings entry: " + name;

    /// <summary>"Also used by" and the other projects.</summary>
    internal static string ReviewSettingsSharedWith(string projects) => "Also used by " + projects;

    /// <summary>The key's source when the entry sets it.</summary>
    internal const string ReviewSettingsFromEntry = "from this entry";

    /// <summary>The key's source when the built-in default gives it.</summary>
    internal const string ReviewSettingsFromDefault = "from default";

    /// <summary>The key's source when an entry it builds on gives it.</summary>
    internal static string ReviewSettingsFromOther(string entry) => "from " + entry;

    /// <summary>"Panels: from default".</summary>
    internal static string ReviewSettingsKeyLine(string key, string from) => key + ": " + from;

    /// <summary>The panels key.</summary>
    internal const string ReviewSettingsPanels = "Panels";

    /// <summary>The groups key.</summary>
    internal const string ReviewSettingsGroups = "File groups";

    /// <summary>The collapse key.</summary>
    internal const string ReviewSettingsCollapse = "Folded files";

    /// <summary>The risky key.</summary>
    internal const string ReviewSettingsRisky = "Risky paths";

    /// <summary>The checks key.</summary>
    internal const string ReviewSettingsChecks = "Required checks";

    /// <summary>Puts a key back on what it inherits.</summary>
    internal const string ReviewSettingsReset = "Reset to default";

    /// <summary>Under a key that was reset.</summary>
    internal const string ReviewSettingsResetNote = "Goes back to the default once you save.";

    /// <summary>Said when the profile changed since the settings were read.</summary>
    internal const string ReviewSettingsStale = "The profile changed since you opened this.";

    /// <summary>Said when a save went through but reading the settings back failed.</summary>
    internal const string ReviewSettingsReadBack = "Saved, but couldn't read the settings back. Reload.";

    /// <summary>Reads the settings again, dropping the edits.</summary>
    internal const string ReviewSettingsReload = "Reload";

    /// <summary>Drops every edit and reads the settings again.</summary>
    internal const string ReviewSettingsDiscard = "Discard changes";

    /// <summary>The primary button.</summary>
    internal const string ReviewSettingsSaveApprove = "Save and approve";

    /// <summary>Said once a save took effect.</summary>
    internal const string ReviewSettingsSavedApproved = "Review settings saved and approved";

    /// <summary>The note after a save that waits for a profile approval; <paramref name="waiting"/> changes wait in all.</summary>
    internal static string ReviewSettingsPending(int waiting) =>
        waiting > 1 ? ReviewsLoginPending + string.Create(CultureInfo.InvariantCulture, $" ({waiting} profile changes wait)") : ReviewsLoginPending;

    /// <summary>Under the settings after a 403.</summary>
    internal const string ReviewSettingsReadOnly = "These settings are read-only here: only the owner changes them.";

    /// <summary>The settings entry's name field.</summary>
    internal const string ReviewSettingsNameField = "Settings entry name";

    /// <summary>The extends field.</summary>
    internal const string ReviewSettingsExtendsField = "Builds on (extends), empty for none";

    /// <summary>The repos field.</summary>
    internal const string ReviewSettingsReposField = "Repos (owner/name, one per line)";

    /// <summary>Under the groups key while the default's by-folder grouping applies.</summary>
    internal const string ReviewSettingsGroupsByFolder = "No groups set: files are grouped by their first two path segments.";

    /// <summary>The collapse field.</summary>
    internal const string ReviewSettingsCollapseField = "Folded file globs, one per line";

    /// <summary>A panel's Show box.</summary>
    internal static string ReviewSettingsShowPanel(string panel) => "Show " + panel;

    /// <summary>A panel's move-up button.</summary>
    internal static string ReviewSettingsMoveUp(string panel) => "Move " + panel + " up";

    /// <summary>A panel's move-down button.</summary>
    internal static string ReviewSettingsMoveDown(string panel) => "Move " + panel + " down";

    /// <summary>The move-up button's glyph text.</summary>
    internal const string ReviewSettingsUp = "Up";

    /// <summary>The move-down button's glyph text.</summary>
    internal const string ReviewSettingsDown = "Down";

    /// <summary>A panel's name in the list.</summary>
    internal static string ReviewSettingsPanelWord(string id) => id switch
    {
        "requiredChecks" => "Required checks",
        "docMap" => "Design doc map",
        "testPlan" => "Test plan",
        "ci" => "CI",
        "evidence" => "Evidence",
        "risk" => "Risk notes",
        "conversation" => "Conversation",
        _ => id,
    };

    /// <summary>A row's field name for a screen reader: "Group 2, name".</summary>
    internal static string ReviewSettingsRowField(string row, int index, string field) =>
        string.Create(CultureInfo.InvariantCulture, $"{row} {index}, {field}");

    /// <summary>A row noun: a group.</summary>
    internal const string ReviewSettingsGroupRow = "Group";

    /// <summary>A row noun: a risky path.</summary>
    internal const string ReviewSettingsRiskRow = "Risky path";

    /// <summary>A row noun: a check.</summary>
    internal const string ReviewSettingsCheckRow = "Check";

    /// <summary>A name field.</summary>
    internal const string ReviewSettingsFieldName = "name";

    /// <summary>A globs field.</summary>
    internal const string ReviewSettingsFieldGlobs = "globs, one per line";

    /// <summary>A reason field.</summary>
    internal const string ReviewSettingsFieldWhy = "why it is risky";

    /// <summary>An id field.</summary>
    internal const string ReviewSettingsFieldId = "id";

    /// <summary>A title field.</summary>
    internal const string ReviewSettingsFieldTitle = "title";

    /// <summary>A when field.</summary>
    internal const string ReviewSettingsFieldWhen = "applies when, globs one per line, empty for always";

    /// <summary>A need field.</summary>
    internal const string ReviewSettingsFieldNeed = "decided by, like ci: ui-tests";

    /// <summary>Adds a group.</summary>
    internal const string ReviewSettingsAddGroup = "Add group";

    /// <summary>Adds a risky path.</summary>
    internal const string ReviewSettingsAddRisk = "Add risky path";

    /// <summary>Adds a check.</summary>
    internal const string ReviewSettingsAddCheck = "Add check";

    /// <summary>Removes a row.</summary>
    internal static string ReviewSettingsRemoveRow(string row, int index) => string.Create(CultureInfo.InvariantCulture, $"Remove {row} {index}");

    /// <summary>Removes a row (the visible word).</summary>
    internal const string ReviewSettingsRemove = "Remove";

    /// <summary>The checks field when a "decided by" can't be read.</summary>
    internal static string ReviewSettingsNeedBad(string id) =>
        $"Check '{id}': write what decides it as ci: <name>, changedWith: <globs>, covers, body: <regex> or manual.";
}
