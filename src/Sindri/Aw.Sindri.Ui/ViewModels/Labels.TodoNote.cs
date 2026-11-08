using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the attention rows of the Todo panel, a both-places conflict and a held mass change (todo spec §3.1, §3.3, §3.4, §3.5, D11, D13).</summary>
internal static partial class Labels
{
    /// <summary>The key drawn beside a folded attention line.</summary>
    internal const string TodoAttnEnterKey = "Enter";

    /// <summary>The folded conflict line's hint, after the Enter key.</summary>
    internal const string TodoAttnSortOutHint = "to sort it out";

    /// <summary>The folded hold line's hint, after the Enter key.</summary>
    internal const string TodoAttnLookHint = "to look";

    /// <summary>The open conflict row's title.</summary>
    internal const string TodoAttnConflictTitle = "Changed in Sindri and in Obsidian";

    /// <summary>The first column's heading of a conflict row.</summary>
    internal const string TodoAttnSindri = "Sindri";

    /// <summary>The second column's heading of a conflict row.</summary>
    internal const string TodoAttnObsidian = "Obsidian";

    /// <summary>The mark on a field that differs between the two versions.</summary>
    internal const string TodoAttnChanged = "changed";

    /// <summary>The label of a version's text.</summary>
    internal const string TodoAttnText = "Text";

    /// <summary>The label of a version's due day.</summary>
    internal const string TodoAttnDue = "Due";

    /// <summary>The label of a version's links.</summary>
    internal const string TodoAttnLinks = "Links";

    /// <summary>The label of a version's state.</summary>
    internal const string TodoAttnState = "State";

    /// <summary>A version that has no due day.</summary>
    internal const string TodoAttnNoDue = "No date";

    /// <summary>A version that has no links.</summary>
    internal const string TodoAttnNoLinks = "No links";

    /// <summary>A version with the todo open.</summary>
    internal const string TodoAttnNotDone = "Not done";

    /// <summary>A version with the todo ticked.</summary>
    internal const string TodoAttnDone = "Done";

    /// <summary>A version that removed the todo.</summary>
    internal const string TodoAttnRemoved = "Removed";

    /// <summary>The button that keeps Sindri's version.</summary>
    internal const string TodoAttnKeepSindri = "Keep Sindri's";

    /// <summary>The button that keeps Obsidian's version.</summary>
    internal const string TodoAttnKeepObsidian = "Keep Obsidian's";

    /// <summary>The button that keeps a removal Sindri made.</summary>
    internal const string TodoAttnKeepRemoved = "Keep it removed";

    /// <summary>The button that takes a removal Obsidian made.</summary>
    internal const string TodoAttnRemoveIt = "Remove it";

    /// <summary>The hold's button that puts the lines back in the note (key 1: Sindri's side).</summary>
    internal const string TodoAttnPutBack = "Put them back in the note";

    /// <summary>The hold's button that applies the changes in Sindri (key 2: the note's side).</summary>
    internal const string TodoAttnApply = "Apply in Sindri too";

    /// <summary>What a conflict row says to X, Space and Delete: nothing is sent.</summary>
    internal const string TodoAttnPickFirst = "Pick a version first";

    /// <summary>The notice after Keep Obsidian's when the note's line moved since the row was drawn (SND-TODO-409).</summary>
    internal const string TodoAttnLineChanged = "The note's line changed since. Here's the latest";

    /// <summary>The notice when a hold pick finds no hold open (SND-TODO-005).</summary>
    internal const string TodoAttnNoHold = "The note has no changes waiting.";

    /// <summary>What Undo says (after "Couldn't undo: ") when there is nothing to undo (SND-TODO-005).</summary>
    internal const string TodoAttnNothingToUndo = "there's nothing to undo";

    /// <summary>The toast after Keep Sindri's.</summary>
    internal const string TodoAttnKeptSindri = "Kept Sindri's version.";

    /// <summary>The toast after Keep Obsidian's.</summary>
    internal const string TodoAttnKeptObsidian = "Kept Obsidian's version.";

    /// <summary>The toast after Keep it removed.</summary>
    internal const string TodoAttnKeptRemoved = "Kept it removed.";

    /// <summary>The toast after Remove it.</summary>
    internal const string TodoAttnRemovedIt = "Removed it.";

    /// <summary>The key a folded attention line draws for the next row.</summary>
    internal const string TodoAttnMoreKey = "Enter";

    /// <summary>A folded conflict line.</summary>
    /// <param name="text">The todo's text (Sindri's side).</param>
    /// <returns>"Changed in Sindri and in Obsidian: reply to Sam."</returns>
    internal static string TodoAttnConflictLine(string text) => $"{TodoAttnConflictTitle}: {text}";

    /// <summary>The line of a hold.</summary>
    /// <param name="count">How many changes are held.</param>
    /// <returns>"The note changed 14 todos."</returns>
    internal static string TodoAttnHoldTitle(int count) => string.Create(CultureInfo.InvariantCulture, $"The note changed {count} {(count == 1 ? "todo" : "todos")}.");

    /// <summary>One held change in the hold's summary.</summary>
    /// <param name="kind">"removed", "edited" or "reopened".</param>
    /// <param name="text">The todo's text.</param>
    /// <returns>"removed 'reply to Sam'", "edited '…'", "un-ticked '…'".</returns>
    internal static string TodoAttnHoldChange(string kind, string text) =>
        (string.Equals(kind, "reopened", StringComparison.Ordinal) ? "un-ticked" : kind) + " '" + text + "'";

    /// <summary>The hold's summary: the first three changes and the rest as a count.</summary>
    /// <param name="shown">The first three, from <see cref="TodoAttnHoldChange"/>.</param>
    /// <param name="more">How many more are held.</param>
    /// <returns>"removed 'a', edited 'b', un-ticked 'c' and 11 more".</returns>
    internal static string TodoAttnHoldSummary(IReadOnlyList<string> shown, int more) =>
        string.Join(", ", shown) + (more > 0 ? string.Create(CultureInfo.InvariantCulture, $" and {more} more") : string.Empty);

    /// <summary>The line under the hold's title in its open form, one per held change.</summary>
    /// <param name="kind">"removed", "edited" or "reopened".</param>
    /// <param name="text">The todo's text.</param>
    /// <returns>"Removed: reply to Sam".</returns>
    internal static string TodoAttnHoldChangeLine(string kind, string text) =>
        (string.Equals(kind, "reopened", StringComparison.Ordinal) ? "Un-ticked" : char.ToUpperInvariant(kind[0]) + kind[1..]) + ": " + text;

    /// <summary>The open hold's list, past its fifth change.</summary>
    /// <param name="more">How many more.</param>
    /// <returns>"and 9 more".</returns>
    internal static string TodoAttnHoldMore(int more) => string.Create(CultureInfo.InvariantCulture, $"and {more} more");

    /// <summary>The conflict part of a field's dimmed line when both versions agree.</summary>
    internal const string TodoAttnSameInBoth = "same in both";

    /// <summary>The slot under a folded attention line that opens the next one.</summary>
    /// <param name="more">How many attention rows wait behind the shown one.</param>
    /// <returns>"Sort out 2 more".</returns>
    internal static string TodoAttnMore(int more) => string.Create(CultureInfo.InvariantCulture, $"Sort out {more} more");

    /// <summary>The same slot for a screen reader.</summary>
    /// <param name="more">How many.</param>
    /// <returns>"2 more to sort out".</returns>
    internal static string TodoAttnMoreName(int more) => string.Create(CultureInfo.InvariantCulture, $"{more} more to sort out");

    /// <summary>A conflict row for a screen reader (§3.5).</summary>
    /// <param name="text">The todo's text.</param>
    /// <returns>The sentence.</returns>
    internal static string TodoAttnConflictName(string text) =>
        $"{TodoAttnConflictTitle}: {text}. Press 1 to keep Sindri's, 2 to keep Obsidian's, Enter for the buttons";

    /// <summary>A hold row for a screen reader: the count and its first three changes.</summary>
    /// <param name="count">How many are held.</param>
    /// <param name="summary">The first three changes.</param>
    /// <returns>The sentence.</returns>
    internal static string TodoAttnHoldName(int count, string summary) =>
        $"The Obsidian note changed {count.ToString(CultureInfo.InvariantCulture)} todos: {summary}. Press 1 to put them back in the note, 2 to apply them in Sindri";

    /// <summary>A conflict button's name for a screen reader.</summary>
    /// <param name="side">"Sindri's" or "Obsidian's".</param>
    /// <param name="text">That version's text, or null when it removed the todo.</param>
    /// <returns>"Keep Sindri's version: reply to Sam" or "…: removed".</returns>
    internal static string TodoAttnKeepName(string side, string? text) => $"Keep {side} version: {text ?? "removed"}";

    /// <summary>The name of the button that takes Obsidian's removal.</summary>
    /// <param name="text">The todo's text in Sindri.</param>
    /// <returns>"Remove the todo: reply to Sam".</returns>
    internal static string TodoAttnRemoveName(string text) => "Remove the todo: " + text;

    /// <summary>The hold's put-back button for a screen reader.</summary>
    /// <param name="count">How many.</param>
    /// <returns>"Put the 14 todos back in the note".</returns>
    internal static string TodoAttnPutBackName(int count) => string.Create(CultureInfo.InvariantCulture, $"Put the {count} todos back in the note");

    /// <summary>The hold's apply button for a screen reader.</summary>
    /// <param name="count">How many.</param>
    /// <returns>"Apply the 14 changes in Sindri".</returns>
    internal static string TodoAttnApplyName(int count) => string.Create(CultureInfo.InvariantCulture, $"Apply the {count} changes in Sindri");

    /// <summary>The toast after Apply.</summary>
    /// <param name="count">How many changes.</param>
    /// <returns>"Applied 14 changes."</returns>
    internal static string TodoAttnApplied(int count) => string.Create(CultureInfo.InvariantCulture, $"Applied {count} changes.");

    /// <summary>The toast after Put them back.</summary>
    /// <param name="count">How many changes.</param>
    /// <returns>"Putting 14 back in the note."</returns>
    internal static string TodoAttnPuttingBack(int count) => string.Create(CultureInfo.InvariantCulture, $"Putting {count} back in the note.");

    /// <summary>The notice when the note changed under a hold pick (SND-TODO-409): the row reloads with the new count.</summary>
    /// <param name="now">How many changes are held now.</param>
    /// <returns>"The note changed again: now 9. Check and pick again."</returns>
    internal static string TodoAttnHoldChangedAgain(int now) => string.Create(CultureInfo.InvariantCulture, $"The note changed again: now {now}. Check and pick again.");

    /// <summary>The notice when the note changed and the hold is gone.</summary>
    internal const string TodoAttnHoldChangedGone = "The note changed again: nothing is held now.";
}
