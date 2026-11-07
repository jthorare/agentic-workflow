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

    /// <summary>The sub line: "acme/api #2071 · feat/retry-budget · a1b2c3d".</summary>
    /// <param name="repo">owner/name.</param>
    /// <param name="number">The PR number.</param>
    /// <param name="branch">The head branch.</param>
    /// <param name="sha7">The short head sha.</param>
    /// <returns>The words.</returns>
    internal static string ReviewFixSubLine(string repo, int number, string branch, string sha7) => string.Create(CultureInfo.InvariantCulture, $"{repo} #{number} · {branch} · {sha7}");

    /// <summary>The PR thread line when Send posts a comment in the review body.</summary>
    /// <param name="number">The PR number.</param>
    /// <returns>The words.</returns>
    internal static string ReviewFixBodyThread(int number) => string.Create(CultureInfo.InvariantCulture, $"Review comment on #{number} (the review body, no diff line), from Sindri, marker sindri.fix:<item id>");

    /// <summary>The PR thread line when Send posts a line comment.</summary>
    /// <param name="path">The file.</param>
    /// <param name="line">The line.</param>
    /// <returns>The words.</returns>
    internal static string ReviewFixLineThread(string path, int line) => string.Create(CultureInfo.InvariantCulture, $"Line comment on {path}:{line}, from Sindri (verdict comment, marker sindri.fix:<item id>)");

    /// <summary>The PR thread line on a PR Sindri does not post on.</summary>
    /// <param name="author">The author.</param>
    /// <returns>The words.</returns>
    internal static string ReviewFixItemOnly(string author) => $"Item only: Sindri won't post on {author}'s PR";

    /// <summary>The outcome when the babysitter took the thread.</summary>
    /// <param name="number">The PR number.</param>
    /// <returns>The words.</returns>
    internal static string ReviewFixBabysitter(int number) => string.Create(CultureInfo.InvariantCulture, $"Fix task sent to the babysitter on #{number}. It takes the thread as work and closes the item when it's fixed.");

    /// <summary>The outcome when a live session took it.</summary>
    /// <param name="branch">The head branch.</param>
    /// <param name="session">The session ("claude, ~/repos/aw-wt/x").</param>
    /// <returns>The words.</returns>
    internal static string ReviewFixSession(string branch, string session) => $"Fix task sent to the session on {branch} ({session}).";

    /// <summary>The outcome when no session is on the branch.</summary>
    /// <param name="branch">The head branch.</param>
    /// <returns>The words.</returns>
    internal static string ReviewFixWaiting(string branch) => $"No session is on {branch}. The item waits in Work as \"Waiting on {branch}\", and the first session on that branch is told.";

    /// <summary>The outcome on a PR Sindri does not post on.</summary>
    /// <param name="author">The author.</param>
    /// <param name="branch">The head branch.</param>
    /// <returns>The words.</returns>
    internal static string ReviewFixItemOnlyOutcome(string author, string branch) => $"{ReviewFixItemOnly(author)}. The item waits in Work as \"Waiting on {branch}\".";

    /// <summary>The posted thread's name: "#2071 · review comment" or "#2071 · X.cs:11".</summary>
    /// <param name="number">The PR number.</param>
    /// <param name="where">"review comment" or "file:line".</param>
    /// <returns>The words.</returns>
    internal static string ReviewFixThread(int number, string where) => string.Create(CultureInfo.InvariantCulture, $"#{number} · {where}");

    /// <summary>The check's second From line.</summary>
    /// <param name="detail">Why it failed.</param>
    /// <returns>The words.</returns>
    internal static string ReviewFixCheckDetail(string detail) => "Required check · " + detail;

    /// <summary>The requirement's second From line.</summary>
    /// <param name="doc">The doc's id.</param>
    /// <returns>The words.</returns>
    internal static string ReviewFixRequirementDetail(string doc) => $"Requirement · {doc} · no PR claims it";

    internal const string ReviewFixAddedLine = "added line";

    /// <summary>Where a fix task from a check or a requirement is posted: the PR's review body.</summary>
    internal const string ReviewFixReviewComment = "review comment";

    internal const string ReviewFixLine = "line";
}
