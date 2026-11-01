using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the page sheet (pages spec §3.6, §5).</summary>
internal static partial class Labels
{
    /// <summary>The mark of a page named in the profile and not approved yet.</summary>
    internal const string ContextPagesWaiting = "Waiting for approval";

    /// <summary>A suggested page's button.</summary>
    internal const string ContextPagesNameThis = "Name this page";

    /// <summary>An owner page's button.</summary>
    internal const string ContextPagesEditPage = "Edit page";

    /// <summary>The section head's button for a page the map didn't find.</summary>
    internal const string ContextPagesNewPage = "New page";

    /// <summary>The top right close affordance and the cancel button.</summary>
    internal const string PageSheetEsc = "Esc";

    /// <summary>The close affordance's name for a screen reader.</summary>
    internal const string PageSheetCloseName = "Close";

    /// <summary>The note when the profile's hash can't be read: the sheet writes nothing without it.</summary>
    internal const string PageSheetNoHash = "Couldn't read your settings just now. Close this and open it again.";

    /// <summary>The caption while no named action relates to a concept: the page saves, but sits under no concept (#1851).</summary>
    internal const string PageSheetUnderNoConcept = "Not under any concept yet. Name an action and tick a concept to list it under one.";

    /// <summary>A saved action's row whose name was cleared: Save writes nothing until it is named or removed (#1853).</summary>
    internal const string PageSheetActionNameNeeded = "Name this action, or remove it";

    /// <summary>The button at the end of the From the map line: swaps the page's map page.</summary>
    internal const string PageSheetPickAnother = "Pick another";

    /// <summary>The search box's name for a screen reader.</summary>
    internal const string PageSheetPickBoxName = "Search the map's pages";

    /// <summary>The line while the search reads.</summary>
    internal const string PageSheetPickSearching = "Searching…";

    /// <summary>The line when the search can't be read.</summary>
    internal const string PageSheetPickFailed = "Couldn't read the map's pages. Try again.";

    /// <summary>The button that closes the search without a pick.</summary>
    internal const string PageSheetPickBack = "Back";

    /// <summary>The line when no page matches the search.</summary>
    /// <param name="query">The text searched.</param>
    /// <returns>The line.</returns>
    internal static string PageSheetPickNone(string query) => "No page matches \"" + query + "\"";

    /// <summary>Pick another's status line when a blank search finds no other page on the map.</summary>
    internal const string PageSheetPickNoPages = "No other pages on the map yet.";

    /// <summary>The Name field's label.</summary>
    internal const string PageSheetNameLabel = "Name";

    /// <summary>The actions' heading.</summary>
    internal const string PageSheetActionsHeading = "Actions";

    /// <summary>An action's name box.</summary>
    internal const string PageSheetActionNameLabel = "Action name";

    /// <summary>An action row's Remove button.</summary>
    internal const string PageSheetActionRemove = "Remove";

    /// <summary>Adds an empty action row.</summary>
    internal const string PageSheetAddAction = "Add an action";

    /// <summary>The picker that adds a concept column.</summary>
    internal const string PageSheetRelateAnother = "Relate to another concept";

    /// <summary>A page with no map page behind it.</summary>
    internal const string PageSheetNoMapPage = "No map page: it is yours alone.";

    /// <summary>The save button when only this page's change waits.</summary>
    internal const string PageSheetSaveAndApprove = "Save and approve";

    /// <summary>The save button when other changes wait, or the profile can't be read.</summary>
    internal const string PageSheetSave = "Save";

    /// <summary>The save button while the write is in flight.</summary>
    internal const string PageSheetSaving = "Saving…";

    /// <summary>The Remove page button.</summary>
    internal const string PageSheetRemovePage = "Remove page";

    /// <summary>The Remove page question's confirm button.</summary>
    internal const string PageSheetRemoveConfirm = "Remove";

    /// <summary>The Remove page question's cancel button.</summary>
    internal const string PageSheetRemoveCancel = "Cancel";

    /// <summary>The discard question.</summary>
    internal const string PageSheetDiscardQuestion = "Discard your changes?";

    /// <summary>Closes the sheet and drops the changes.</summary>
    internal const string PageSheetDiscard = "Discard";

    /// <summary>Goes back to the fields.</summary>
    internal const string PageSheetKeepEditing = "Keep editing";

    /// <summary>The line while the page is read.</summary>
    internal const string PageSheetLoading = "Reading the page…";

    /// <summary>The line when the page can't be read.</summary>
    internal const string PageSheetUnreadable = "Couldn't read this page just now. Close this and try again.";

    /// <summary>The line under the buttons when the profile moved since the sheet opened (SND-PROFILE-003).</summary>
    internal const string PageSheetClash = "The profile changed since you opened this. Your changes are kept: Save again to apply them on top.";

    /// <summary>The line under the buttons when the write couldn't be made.</summary>
    internal const string PageSheetFailed = "Couldn't save just now. Try again.";

    /// <summary>The warning under the Name field (it doesn't block the save).</summary>
    /// <param name="name">The other page's name.</param>
    /// <returns>The line.</returns>
    internal static string PageSheetSameName(string name) => "Another page is called " + name;

    /// <summary>The Remove page question.</summary>
    /// <param name="name">The page's name.</param>
    /// <returns>The question.</returns>
    internal static string PageSheetRemoveQuestion(string name) =>
        "Remove the page " + name + "? Its actions and their concept links go too; the map's own page stays as a suggestion.";

    /// <summary>A concept tick's name.</summary>
    /// <param name="concept">The concept's name.</param>
    /// <returns>"Relates to Approval workflow".</returns>
    internal static string PageSheetTick(string concept) => "Relates to " + concept;

    /// <summary>An action row as a screen reader reads it.</summary>
    /// <param name="number">The row's number, from 1.</param>
    /// <param name="total">How many rows there are.</param>
    /// <param name="name">The action's name.</param>
    /// <param name="concepts">How many concepts it relates to.</param>
    /// <returns>"Action 2 of 3, Submit a schedule, relates to 2 concepts".</returns>
    internal static string PageSheetActionRow(int number, int total, string name, int concepts) =>
        string.Create(CultureInfo.InvariantCulture, $"Action {number} of {total}, {(name.Length == 0 ? "no name yet" : name)}, relates to {concepts} {(concepts == 1 ? "concept" : "concepts")}");
}
