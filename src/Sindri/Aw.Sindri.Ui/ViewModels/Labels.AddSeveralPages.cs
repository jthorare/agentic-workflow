using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Add several pages sheet (pages spec §3.6, §5).</summary>
internal static partial class Labels
{
    /// <summary>The Pages and actions section's button, and the sheet's title.</summary>
    internal const string AddSeveralPages = "Add several pages";

    /// <summary>The paste box's label.</summary>
    internal const string AddSeveralPagesBoxLabel = "Pages to add";

    /// <summary>The paste box's description.</summary>
    internal const string AddSeveralPagesBoxHelp = "One page per block: its name, then one action per line, optionally -> concept, concept";

    /// <summary>The button that reads the paste.</summary>
    internal const string AddSeveralPagesRead = "Read the list";

    /// <summary>Under the box when nothing was pasted (no call is made).</summary>
    internal const string AddSeveralPagesNothingPasted = "Paste a list first: one page per block.";

    /// <summary>Under the box when the server can't be asked.</summary>
    internal const string AddSeveralPagesUnreadable = "Couldn't read the list just now. Try again.";

    /// <summary>A block that matches no suggested page.</summary>
    internal const string AddSeveralPagesNewPage = "New page";

    /// <summary>A block that would replace a page the owner already named.</summary>
    internal const string AddSeveralPagesReplaces = "Replaces a page you already named";

    /// <summary>The save button when only these pages wait.</summary>
    internal const string AddSeveralPagesSaveAllAndApprove = "Save all and approve";

    /// <summary>The save button when other changes wait, or the profile can't be read.</summary>
    internal const string AddSeveralPagesSaveAll = "Save all";

    /// <summary>The cancel button and close affordance.</summary>
    internal const string AddSeveralPagesEsc = "Esc";

    /// <summary>The close affordance's name for a screen reader.</summary>
    internal const string AddSeveralPagesCloseName = "Close";

    /// <summary>The line under the buttons when the write couldn't be made.</summary>
    internal const string AddSeveralPagesFailed = "Couldn't save just now. Try again.";

    /// <summary>The line under the buttons when the profile moved since the list was read (SND-PROFILE-003).</summary>
    internal const string AddSeveralPagesClash = "The profile changed since you read the list. Your ticks and names are kept: Save again to apply them on top.";

    /// <summary>A matched block's line.</summary>
    /// <param name="name">The suggested page's name.</param>
    /// <param name="actions">How many actions it has on the map.</param>
    /// <returns>"Matches Schedule week (4 actions found)".</returns>
    internal static string AddSeveralPagesMatches(string name, int actions) =>
        string.Create(CultureInfo.InvariantCulture, $"Matches {name} ({actions} {(actions == 1 ? "action" : "actions")} found)");

    /// <summary>A block that names the same page as an earlier one.</summary>
    /// <param name="block">The earlier block's number.</param>
    /// <returns>"Same page as block 1".</returns>
    internal static string AddSeveralPagesSameAs(int block) => string.Create(CultureInfo.InvariantCulture, $"Same page as block {block}");

    /// <summary>An action line that names an action of the matched page.</summary>
    /// <param name="action">The action's name on the map.</param>
    /// <returns>"Matches Submit schedule".</returns>
    internal static string AddSeveralPagesActionMatches(string action) => "Matches " + action;

    /// <summary>A row's tick, for a screen reader.</summary>
    /// <param name="name">The page's name.</param>
    /// <returns>"Add Schedule week".</returns>
    internal static string AddSeveralPagesTick(string name) => "Add " + name;

    /// <summary>What the server's reason starts with when a block names a concept that matches none.</summary>
    internal const string AddSeveralPagesUnknownConceptStart = "No concept called";

    /// <summary>The line beside the picker of a concept name that matched none.</summary>
    /// <param name="name">The name as pasted.</param>
    /// <returns>"No concept called "aproval": pick one".</returns>
    internal static string AddSeveralPagesUnknownConcept(string name) => "No concept called \"" + name + "\": pick one";

    /// <summary>The picker's placeholder.</summary>
    internal const string AddSeveralPagesPickPlaceholder = "Pick a concept";

    /// <summary>The picker's name for a screen reader.</summary>
    /// <param name="name">The name as pasted.</param>
    /// <returns>"Concept for aproval".</returns>
    internal static string AddSeveralPagesPickName(string name) => "Concept for " + name;
}
