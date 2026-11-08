namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the inbox's action bar (F3 needs-you spec §6). No em dashes, no internal terms.</summary>
internal static partial class Labels
{
    /// <summary>The primary action of a desk request.</summary>
    internal const string NeedsYouApprove = "Approve";

    /// <summary>The primary action while it asks first (the request was not read to the end).</summary>
    internal const string NeedsYouApproveAsk = "Approve?";

    /// <summary>Said when Sindri answered but did not approve the profile.</summary>
    internal const string NeedsYouNotApproved = "Sindri did not approve it. Ask again from the box.";

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

    /// <summary>The done card's title after an approval.</summary>
    /// <param name="item">What was approved.</param>
    /// <returns>The words.</returns>
    internal static string NeedsYouApprovedTitle(string item) => "Approved: " + item;

    /// <summary>The done card's sentence after an approval.</summary>
    internal const string NeedsYouApprovedBody = "It is locked in. The next stage has started from exactly this.";

    /// <summary>The bar's line under a settings or workflow approval.</summary>
    internal const string NeedsYouKeepSettings = "Leave it here to keep the current settings.";

    /// <summary>Why send back is off for a settings or workflow approval.</summary>
    internal const string NeedsYouSendBackLocked = "These can only be approved. Change the settings or workflow file to undo.";

    /// <summary>The card's More menu.</summary>
    internal const string NeedsYouMore = "More";

    /// <summary>The key hints, after the keys.</summary>
    internal const string NeedsYouKeyOpenWord = "open";

    /// <summary>The key hint of A.</summary>
    internal const string NeedsYouKeyApproveWord = "approve";

    /// <summary>The key hint of D.</summary>
    internal const string NeedsYouKeySendBackWord = "send back";

    /// <summary>The bar's accessible name for a button: the words and what they act on.</summary>
    /// <param name="action">The words.</param>
    /// <param name="subject">The request's title.</param>
    /// <returns>The name.</returns>
    internal static string NeedsYouBarName(string action, string subject) => $"{action} {subject}";

    /// <summary>The More menu's accessible name.</summary>
    /// <param name="subject">The request's title.</param>
    /// <returns>The name.</returns>
    internal static string NeedsYouMoreName(string subject) => $"More actions for {subject}";

    /// <summary>The action bar's accessible name.</summary>
    /// <param name="subject">The request's title.</param>
    /// <returns>The name.</returns>
    internal static string NeedsYouBarGroupName(string subject) => $"Actions for {subject}";

}
