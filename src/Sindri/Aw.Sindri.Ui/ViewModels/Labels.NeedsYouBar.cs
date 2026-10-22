namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the inbox's action bar (F3 needs-you spec §6). No em dashes, no internal terms.</summary>
internal static partial class Labels
{
    /// <summary>The primary action of a desk request.</summary>
    internal const string NeedsYouApprove = "Approve";

    /// <summary>The primary action while it asks first (the request was not read to the end).</summary>
    internal const string NeedsYouApproveAsk = "Approve?";

    /// <summary>Said when the typed challenge ran out before it was sent: a new phrase is shown.</summary>
    internal const string NeedsYouTypedExpired = "That took too long. A new phrase is shown: type it again.";

    /// <summary>Said when Sindri answered but did not approve the profile.</summary>
    internal const string NeedsYouNotApproved = "Sindri did not approve it. Ask again from the box.";

    /// <summary>Said when the workflow a row names is not in the list Sindri serves any more.</summary>
    internal const string NeedsYouRecipeGone = "Sindri doesn't list that workflow any more. Reload and try again.";

    /// <summary>The secondary action of a request.</summary>
    internal const string NeedsYouSendBack = "Send back";

    /// <summary>The secondary action of a decision, when the decision serves no word of its own.</summary>
    internal const string NeedsYouDecline = "Decline";

    /// <summary>What a second A confirms.</summary>
    internal const string NeedsYouConfirmHint = "You have not read to the end. Press A again to approve.";

    /// <summary>The caption of the reason box.</summary>
    internal const string NeedsYouReasonLabel = "Why are you sending it back?";

    /// <summary>What the reason box says about the text, for a request that is sent back.</summary>
    internal const string NeedsYouReasonSentBack = "Required. It goes with the request as your notes.";

    /// <summary>What the reason box says about the text, for a decision that is declined.</summary>
    internal const string NeedsYouReasonRecorded = "Required. The reason is recorded.";

    /// <summary>The reason box's placeholder, for a request.</summary>
    internal const string NeedsYouReasonPlaceholder = "What should change?";

    /// <summary>The reason box's placeholder, for a decision.</summary>
    internal const string NeedsYouReasonPlaceholderDecline = "Why decline? It is recorded with the item.";

    /// <summary>The reason box's accessible name.</summary>
    internal const string NeedsYouReasonName = "Reason";

    /// <summary>Closes the reason box or the drop question.</summary>
    internal const string NeedsYouCancel = "Cancel";

    /// <summary>The bar's line after an approval.</summary>
    internal const string NeedsYouApprovedHint = "Approved. The row has left your inbox.";

    /// <summary>The bar's line after a send back.</summary>
    internal const string NeedsYouSentBackHint = "Sent back. The row has left your inbox.";

    /// <summary>The bar's line after a drop.</summary>
    internal const string NeedsYouDroppedHint = "Dropped. The row has left your inbox.";

    /// <summary>The done card's title after an approval.</summary>
    /// <param name="item">What was approved.</param>
    /// <returns>The words.</returns>
    internal static string NeedsYouApprovedTitle(string item) => "Approved: " + item;

    /// <summary>The done card's sentence after an approval.</summary>
    internal const string NeedsYouApprovedBody = "It is locked in. The next stage has started from exactly this.";

    /// <summary>The done card's title after a send back.</summary>
    /// <param name="item">What was sent back.</param>
    /// <returns>The words.</returns>
    internal static string NeedsYouSentBackTitle(string item) => "Sent back: " + item;

    /// <summary>The done card's sentence after a send back.</summary>
    internal const string NeedsYouSentBackBody = "Your reason went with it.";

    /// <summary>The done card's title after a drop.</summary>
    /// <param name="item">What was dropped.</param>
    /// <returns>The words.</returns>
    internal static string NeedsYouDroppedTitle(string item) => "Dropped: " + item;

    /// <summary>The done card's sentence after a drop.</summary>
    internal const string NeedsYouDroppedBody = "This run has ended.";

    /// <summary>The bar's line after the terminal command was copied.</summary>
    internal const string NeedsYouCopied = "Copied. Paste it in your terminal to approve.";

    /// <summary>The bar's line under a merge: the command is the user's to run.</summary>
    internal const string NeedsYouTerminalOnly = "Only you can approve a merge, and only from your terminal. The button copies the command.";

    /// <summary>The bar's line under any other protected action.</summary>
    internal const string NeedsYouProtectedHint = "Protected: only you can do this, from a terminal.";

    /// <summary>The bar's line under a settings or workflow approval.</summary>
    internal const string NeedsYouKeepSettings = "Leave it here to keep the current settings.";

    /// <summary>Why send back is off for a settings or workflow approval.</summary>
    internal const string NeedsYouSendBackLocked = "These can only be approved. Change the settings or workflow file to undo.";

    /// <summary>The terminal button's words: the action and where it goes.</summary>
    /// <param name="action">The action's words.</param>
    /// <returns>The words.</returns>
    internal static string NeedsYouTerminalButton(string action) => action + " · terminal";

    /// <summary>The terminal button's accessible name.</summary>
    /// <param name="action">The action's words.</param>
    /// <param name="subject">What it acts on.</param>
    /// <returns>The name.</returns>
    internal static string NeedsYouTerminalName(string action, string subject) => $"{action} {subject}, copies a command to run in your terminal";

    /// <summary>The words of a merge's terminal button.</summary>
    internal const string NeedsYouApproveMerge = "Approve merge";

    /// <summary>The words of the terminal button for more tries.</summary>
    internal const string NeedsYouMoreTries = "Allow more tries";

    /// <summary>The placeholder for a feature that is not built yet.</summary>
    internal const string NeedsYouEditLater = "Edit before approve";

    /// <summary>The chip on it.</summary>
    internal const string NeedsYouNotAvailableYet = "Not available yet";

    /// <summary>The card's More menu.</summary>
    internal const string NeedsYouMore = "More";

    /// <summary>The menu item that drops a request.</summary>
    internal const string NeedsYouDrop = "Drop this item";

    /// <summary>The question under the bar before a drop is sent.</summary>
    internal const string NeedsYouDropAsk = "Drop this item? This run ends.";

    /// <summary>The button that sends the drop.</summary>
    internal const string NeedsYouDropConfirm = "Drop";

    /// <summary>What the bar says when the request has no open run any more.</summary>
    internal const string NeedsYouNotOpen = "This request is no longer open.";

    /// <summary>The key hints, after the keys.</summary>
    internal const string NeedsYouKeyOpenWord = "open";

    /// <summary>The key hint of A.</summary>
    internal const string NeedsYouKeyApproveWord = "approve";

    /// <summary>The key hint of D.</summary>
    internal const string NeedsYouKeySendBackWord = "send back";

    /// <summary>The key hint of J after an approval.</summary>
    internal const string NeedsYouKeyNextWord = "next";

    /// <summary>The bar's accessible name for a button: the words and what they act on.</summary>
    /// <param name="action">The words.</param>
    /// <param name="subject">The request's title.</param>
    /// <returns>The name.</returns>
    internal static string NeedsYouBarName(string action, string subject) => $"{action} {subject}";

    /// <summary>The More menu's accessible name.</summary>
    /// <param name="subject">The request's title.</param>
    /// <returns>The name.</returns>
    internal static string NeedsYouMoreName(string subject) => $"More actions for {subject}";

    /// <summary>What hovering the terminal button says.</summary>
    internal const string NeedsYouTerminalTip = "Copies a command to run in your terminal";

    /// <summary>The action bar's accessible name.</summary>
    /// <param name="subject">The request's title.</param>
    /// <returns>The name.</returns>
    internal static string NeedsYouBarGroupName(string subject) => $"Actions for {subject}";
}
