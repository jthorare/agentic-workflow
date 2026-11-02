using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Add several pages sheet (pages spec §3.6, §5).</summary>
internal static partial class Labels
{
    /// <summary>The Pages and actions section's button, and the sheet's title.</summary>
    internal const string AddSeveralPages = "Add several pages";

    /// <summary>The section's button that opens Accept all suggested.</summary>
    /// <param name="count">How many suggested pages the section lists.</param>
    /// <returns>"Accept all suggested (14)".</returns>
    internal static string AcceptAllButton(int count) => string.Create(CultureInfo.InvariantCulture, $"Accept all suggested ({count})");

    /// <summary>The first choice of a page's concept picker: add the page under no concept.</summary>
    internal const string AcceptAllNone = "None";

    /// <summary>The reason of a row whose picker says None.</summary>
    internal const string AcceptAllNoPick = "Not added: no concept picked";

    /// <summary>The reason of a page with no actions to link a concept to.</summary>
    internal const string AcceptAllNoActions = "Not added: this page has no actions to link a concept to";

    /// <summary>The Accept all suggested sheet's title.</summary>
    internal const string AcceptAllSuggested = "Accept all suggested";

    /// <summary>The line under the rows when more suggested pages wait than the sheet lists.</summary>
    /// <param name="more">How many suggested pages are not listed.</param>
    /// <returns>"186 more: accept these first".</returns>
    internal static string AcceptAllMore(int more) => string.Create(CultureInfo.InvariantCulture, $"{more} more: accept these first");

    /// <summary>The button that opens a row into its fields.</summary>
    internal const string AcceptAllEdit = "Edit";

    /// <summary>The open button's name for a screen reader.</summary>
    /// <param name="name">The page's name.</param>
    /// <returns>"Edit Schedule week".</returns>
    internal static string AcceptAllEditName(string name) => "Edit " + name;

    /// <summary>The open row's name box label.</summary>
    internal const string AcceptAllNameLabel = "Page name";

    /// <summary>The reason of a row past the profile's page cap.</summary>
    internal const string AcceptAllProfileFull = "The profile holds at most 200 pages";

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
