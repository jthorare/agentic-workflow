using System.Globalization;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the review page, its panels and strip (review page spec §6.2-6.4, §16; mockups review-a-*).</summary>
internal static partial class Labels
{
    /// <summary>The panels' titles.</summary>
    internal const string ReviewPanelRequiredChecks = "Required checks";

    internal const string ReviewPanelDocMap = "Design doc";

    internal const string ReviewPanelTestPlan = "Test plan";

    internal const string ReviewPanelCi = "CI";

    internal const string ReviewPanelEvidence = "Evidence";

    internal const string ReviewPanelRisk = "Risk";

    internal const string ReviewPanelConversation = "Conversation";

    /// <summary>A panel's one-line placeholder while it loads.</summary>
    internal const string ReviewPanelReading = "Reading…";

    /// <summary>The strip's four placeholders while the page loads (mockup review-a-panels-loading).</summary>
    internal const string ReviewStripReadingChecks = "Reading checks…";

    internal const string ReviewStripReadingClaim = "Reading the claim…";

    internal const string ReviewStripReadingRisk = "Reading risk…";

    internal const string ReviewStripReadingCi = "Reading CI…";

    /// <summary>The required check states' words (never color alone).</summary>
    internal const string ReviewCheckFail = "fail";

    internal const string ReviewCheckNeedsTick = "needs a tick";

    internal const string ReviewCheckRunning = "running";

    internal const string ReviewCheckClaimed = "claimed in body";

    internal const string ReviewCheckPass = "pass";

    /// <summary>The button on a failed check, an unclaimed requirement and a diff line.</summary>
    internal const string ReviewSendFix = "Send as fix task";

    /// <summary>The doc map's marks.</summary>
    internal const string ReviewDocThisPr = "this PR";

    internal const string ReviewDocUnclaimed = "unclaimed";

    /// <summary>The header's button and the review buttons that open GitHub (release 1 posts nothing).</summary>
    internal const string ReviewOpenOnGitHub = "Open on GitHub";

    internal const string ReviewApproveOnGitHub = "Approve on GitHub";

    internal const string ReviewRequestChangesOnGitHub = "Request changes on GitHub";

    internal const string ReviewCommentOnGitHub = "Comment on GitHub";

    internal const string ReviewOwnPr = "You can't approve or request changes on your own PR: GitHub takes a comment only";

    internal const string ReviewActionNote = "Release 1: both buttons open this PR's review box on GitHub. Your ticks are saved on this head.";

    /// <summary>The page's whole-page states (mockups review-a-page-*).</summary>
    internal const string ReviewNotFoundHeading = "Pull request not found";

    internal const string ReviewNotSyncedHeading = "Sindri doesn't sync this repo";

    internal const string ReviewOfflineHeading = "Offline, and this PR isn't cached yet";

    internal const string ReviewLoginHeading = "Set your GitHub login";

    internal const string ReviewBack = "Reviews";

    internal const string ReviewTryAgain = "Try again";

    /// <summary>The diff's words.</summary>
    internal const string ReviewDiffTitle = "Files changed";

    internal const string ReviewDiffReading = "Reading files…";

    internal const string ReviewDiffShow = "Show";

    internal const string ReviewDiffKeys = "j/k file · n/p hunk · c fix task · o fold";

    /// <summary>The settings chip: "Settings: sindri".</summary>
    /// <param name="name">The settings name.</param>
    /// <returns>The chip's words.</returns>
    internal static string ReviewSettings(string name) => "Settings: " + name;

    /// <summary>"head a1b2c3d".</summary>
    /// <param name="sha7">The short sha.</param>
    /// <returns>The words.</returns>
    internal static string ReviewHead(string sha7) => "head " + sha7;

    /// <summary>A required check's screen-reader name: its state and reason ("Recipe change shows Ui tests ran: fail, ui-tests: failure on a1b2c3d").</summary>
    /// <param name="title">The check's title.</param>
    /// <param name="status">The state's word.</param>
    /// <param name="detail">The reason; null for none.</param>
    /// <returns>The name.</returns>
    internal static string ReviewCheckName(string title, string status, string? detail) => detail is { Length: > 0 } ? $"{title}: {status}, {detail}" : $"{title}: {status}";

    /// <summary>"1 file" or "42 files".</summary>
    /// <param name="count">The count.</param>
    /// <returns>The words.</returns>
    internal static string ReviewFiles(int count) => count == 1 ? "1 file" : string.Create(CultureInfo.InvariantCulture, $"{count:N0} files");

    /// <summary>A folded group's name: "Lockfiles, collapsed, 14 files".</summary>
    /// <param name="name">The group.</param>
    /// <param name="count">Its files.</param>
    /// <returns>The name.</returns>
    internal static string ReviewGroupFolded(string name, int count) => $"{name}, collapsed, {ReviewFiles(count)}";

    /// <summary>An open group's name: "Core, 3 files".</summary>
    /// <param name="name">The group.</param>
    /// <param name="count">Its files.</param>
    /// <returns>The name.</returns>
    internal static string ReviewGroupOpen(string name, int count) => $"{name}, {ReviewFiles(count)}";

    /// <summary>A hunk header's name: "lines 40 to 48".</summary>
    /// <param name="from">The first new-file line.</param>
    /// <param name="to">The last.</param>
    /// <returns>The name.</returns>
    internal static string ReviewHunkName(int from, int to) => string.Create(CultureInfo.InvariantCulture, $"lines {from} to {to}");

    /// <summary>A diff line's name: "line 42, added: text", "line 40, removed: text" or "line 41: text".</summary>
    /// <param name="number">The line number.</param>
    /// <param name="kind">What the line is.</param>
    /// <param name="text">The line's text.</param>
    /// <returns>The name.</returns>
    internal static string ReviewLineName(int number, ReviewDiffLineKind kind, string text) => kind switch
    {
        ReviewDiffLineKind.Added => string.Create(CultureInfo.InvariantCulture, $"line {number}, added: {text}"),
        ReviewDiffLineKind.Removed => string.Create(CultureInfo.InvariantCulture, $"line {number}, removed: {text}"),
        ReviewDiffLineKind.Context => string.Create(CultureInfo.InvariantCulture, $"line {number}: {text}"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, message: null),
    };

    /// <summary>The file's stat: "+4 −1".</summary>
    /// <param name="additions">Lines added.</param>
    /// <param name="deletions">Lines removed.</param>
    /// <returns>The words.</returns>
    internal static string ReviewStat(int additions, int deletions) => string.Create(CultureInfo.InvariantCulture, $"+{additions} −{deletions}");
}
