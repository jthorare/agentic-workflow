using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the fix-task sheet (review page spec §8.2, §16 "Wording the mockups set"; mockups sheet-fix-*).</summary>
internal static partial class Labels
{
    internal const string ReviewFixHeading = "Send as fix task";

    internal const string ReviewFixFrom = "From";

    internal const string ReviewFixWhatNeeds = "What needs fixing";

    internal const string ReviewFixWhatSendDoes = "What Send does";

    internal const string ReviewFixSend = "Send";

    internal const string ReviewFixSending = "Sending…";

    internal const string ReviewFixCancel = "Cancel";

    internal const string ReviewFixRetry = "Retry post";

    internal const string ReviewFixClose = "Close";

    internal const string ReviewFixSentHeading = "Fix task sent";

    internal const string ReviewFixHalfHeading = "Fix task not fully sent";

    internal const string ReviewFixNotPosted = "not posted";

    internal const string ReviewFixNotPostedContributor = "not posted (not your PR)";

    internal const string ReviewFixRetryNote = "Retry reuses the item and the same marker, so the thread can't post twice.";

    /// <summary>The Goes to line of "What Send does".</summary>
    /// <param name="number">The PR number.</param>
    /// <param name="branch">The head branch.</param>
    /// <returns>The words.</returns>
    internal static string ReviewFixGoesTo(int number, string branch) => string.Create(CultureInfo.InvariantCulture, $"The babysitter watching #{number}, else a live session on {branch}, else the item waits in Work");

    /// <summary>The check's From box: its state word and title, and its second line.</summary>
    /// <param name="status">The state's word.</param>
    /// <param name="title">The check's title.</param>
    /// <returns>The first line.</returns>
    internal static string ReviewFixCheckTitle(string status, string title) => $"{status}  {title}";

    /// <summary>The pre-filled text for a failed check.</summary>
    /// <param name="title">The check's title.</param>
    /// <param name="detail">Why it failed; empty for none.</param>
    /// <returns>The text.</returns>
    internal static string ReviewFixCheckText(string title, string detail) =>
        detail.Length > 0 ? $"Required check failed: {title}. {detail}. Fix it, or say why it doesn't apply here." : $"Required check failed: {title}. Fix it, or say why it doesn't apply here.";

    /// <summary>The pre-filled text for an unclaimed requirement.</summary>
    /// <param name="id">The requirement's id.</param>
    /// <param name="title">Its title.</param>
    /// <param name="doc">The doc's id.</param>
    /// <returns>The text.</returns>
    internal static string ReviewFixRequirementText(string id, string title, string doc) => $"{id} ({title}) in {doc} isn't covered by this PR. Cover it, or say why it waits.";

    /// <summary>The item's title: "Fix: " and the first line, cut at 44 characters with an ellipsis.</summary>
    /// <param name="firstLine">The text's first line.</param>
    /// <returns>The title.</returns>
    internal static string ReviewFixItemTitle(string firstLine) => "Fix: " + (firstLine.Length > 44 ? firstLine[..43] + "…" : firstLine);

    /// <summary>"132 / 2,000".</summary>
    /// <param name="length">The text's length.</param>
    /// <returns>The count.</returns>
    internal static string ReviewFixCount(int length) => string.Create(CultureInfo.InvariantCulture, $"{length:N0} / 2,000");
}
