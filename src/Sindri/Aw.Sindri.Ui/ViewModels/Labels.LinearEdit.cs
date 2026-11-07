using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Linear issue sheet's edit form, its banners and its discard question (Linear hub spec §3 and §8).</summary>
internal static partial class Labels
{
    /// <summary>The Save button.</summary>
    internal const string LinearSave = "Save";

    /// <summary>The comment box's Post button.</summary>
    internal const string LinearPost = "Post";

    /// <summary>The line after a posted comment.</summary>
    internal const string LinearPosted = "Posted";

    /// <summary>The line after a posted comment Linear was slow to answer.</summary>
    internal const string LinearPostedSlow = "Posted (Linear was slow to answer)";

    /// <summary>The line after a comment with no answer; Retry sits beside it.</summary>
    internal const string LinearCommentUnknown = "Linear didn't confirm the comment. Retry?";

    /// <summary>The line under a comment over the cap.</summary>
    internal const string LinearCommentCap = "Comments can be up to 65,536 characters.";

    /// <summary>The discard question's line for a typed comment.</summary>
    internal const string LinearCommentNotPosted = "Your comment isn't posted yet.";

    /// <summary>The comment box's accessible name.</summary>
    internal const string LinearCommentName = "Comment";

    /// <summary>The comment box's placeholder.</summary>
    internal const string LinearCommentPlaceholder = "Write a comment";

    /// <summary>The Save button while the request runs.</summary>
    internal const string LinearSending = "Sending…";

    /// <summary>The title box's label (and its name in the dirty line).</summary>
    internal const string LinearIssueTitle = "Title";

    /// <summary>The clear entry of the cycle picker.</summary>
    internal const string LinearNoCycle = "No cycle";

    /// <summary>The Connect button of the write-access banner.</summary>
    internal const string LinearConnectButton = "Connect";

    /// <summary>The banner over an issue Sindri may not write to for want of a write key.</summary>
    internal const string LinearWriteAccess = "Edits need write access.";

    /// <summary>The line after an edit Linear already had.</summary>
    internal const string LinearAlreadySaved = "Already saved. Linear has your change.";

    /// <summary>The line after an edit Linear took but Sindri could not store yet.</summary>
    internal const string LinearSavedAfterSync = "Saved in Linear; Sindri shows it after the next sync.";

    /// <summary>The line after a save with no answer; Retry sits beside it.</summary>
    internal const string LinearUnknown = "Linear didn't confirm it. Retry?";

    /// <summary>The Retry button.</summary>
    internal const string LinearRetry = "Retry";

    /// <summary>The conflict banner's second line.</summary>
    internal const string LinearConflictNote = "Nothing was sent. The fields below differ from what you opened; Linear's value is next to yours.";

    /// <summary>One conflicting field's button.</summary>
    internal const string LinearUseLinears = "Use Linear's";

    /// <summary>The conflict banner's button that takes Linear's value for every listed field.</summary>
    internal const string LinearUseLinearsAll = "Use Linear's for all";

    /// <summary>The conflict banner's button that sends the owner's values again against what Linear just showed.</summary>
    internal const string LinearSaveMine = "Save mine anyway";

    /// <summary>The discard question's way back.</summary>
    internal const string LinearKeepEditing = "Keep editing";

    /// <summary>The discard question's way out.</summary>
    internal const string LinearDiscard = "Discard";

    /// <summary>The line after an applied save.</summary>
    /// <param name="time">When Linear says the issue changed, HH:mm.</param>
    /// <returns>"Saved. The fields show Linear's answer (10:06)."</returns>
    internal static string LinearSaved(string time) => $"Saved. The fields show Linear's answer ({time}).";

    /// <summary>The line after 15 s without an answer.</summary>
    /// <param name="key">The issue.</param>
    /// <returns>"Linear didn't confirm it. Checking ENG-12…"</returns>
    internal static string LinearChecking(string key) => $"Linear didn't confirm it. Checking {key}…";

    /// <summary>The conflict banner.</summary>
    /// <param name="key">The issue.</param>
    /// <param name="time">When Linear changed it, HH:mm.</param>
    /// <returns>"ENG-12 changed in Linear at 10:01."</returns>
    internal static string LinearConflictHeadline(string key, string time) => $"{key} changed in Linear at {time}.";

    /// <summary>One conflicting field's values.</summary>
    /// <param name="linear">What Linear has.</param>
    /// <param name="yours">What the owner picked.</param>
    /// <returns>"Linear: Medium · yours: High".</returns>
    internal static string LinearConflictText(string linear, string yours) => $"Linear: {linear} · yours: {yours}";

    /// <summary>The banner after a rate limit.</summary>
    /// <param name="time">When to try again, HH:mm.</param>
    /// <returns>"Linear's rate limit: try again after 10:47".</returns>
    internal static string LinearRateLimited(string time) => $"Linear's rate limit: try again after {time}";

    /// <summary>The banner over an issue of a team the owner did not pick.</summary>
    /// <param name="team">The team key.</param>
    /// <returns>"ENG isn't one of your teams in Sindri."</returns>
    internal static string LinearTeamNotPicked(string team) => $"{team} isn't one of your teams in Sindri.";

    /// <summary>The dirty line beside Save.</summary>
    /// <param name="fields">The names of the changed fields, in grid order.</param>
    /// <returns>"2 changes: State, Priority".</returns>
    internal static string LinearDirtyLine(IReadOnlyList<string> fields) =>
        fields.Count == 0 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"{fields.Count} {(fields.Count == 1 ? "change" : "changes")}: ") + string.Join(", ", fields);

    /// <summary>The discard question.</summary>
    /// <param name="key">The issue.</param>
    /// <returns>"Discard changes to ENG-12?"</returns>
    internal static string LinearDiscardHeading(string key) => $"Discard changes to {key}?";

    /// <summary>The discard question's body.</summary>
    /// <param name="fields">The names of the changed fields.</param>
    /// <param name="comment">True when a typed comment is not posted.</param>
    /// <returns>"Your edits to State and Priority aren't saved yet.", "Your comment isn't posted yet." or both, the edits first.</returns>
    internal static string LinearDiscardBody(IReadOnlyList<string> fields, bool comment)
    {
        var edits = fields.Count == 0 ? string.Empty : "Your edits to " + (fields.Count == 1 ? fields[0] : string.Join(", ", fields.Take(fields.Count - 1)) + " and " + fields[^1]) + " aren't saved yet.";
        return comment ? (edits.Length == 0 ? LinearCommentNotPosted : edits + " " + LinearCommentNotPosted) : edits;
    }

    /// <summary>A picker that cannot offer choices yet.</summary>
    /// <param name="team">The team key.</param>
    /// <param name="what">states, members, cycles or labels.</param>
    /// <returns>"ENG's states haven't synced yet".</returns>
    internal static string LinearLookupNotSynced(string team, string what) => $"{team}'s {what} haven't synced yet";

    /// <summary>A picker whose choices could not be read.</summary>
    /// <param name="team">The team key.</param>
    /// <param name="what">states, members, cycles or labels.</param>
    /// <returns>"Couldn't load ENG's states".</returns>
    internal static string LinearLookupFailed(string team, string what) => $"Couldn't load {team}'s {what}";

    /// <summary>A field's name in the dirty line and the discard question.</summary>
    /// <param name="value">The field's name and value.</param>
    /// <returns>"State: Done, changed".</returns>
    internal static string LinearChangedName(string value) => value + ", changed";

    /// <summary>The banner for a refused save (Linear hub spec §8).</summary>
    /// <param name="error">The refusal.</param>
    /// <param name="team">The issue's team key.</param>
    /// <param name="key">The issue.</param>
    /// <returns>§8's text for the code; the server's own message for a code whose text names a value only it knows.</returns>
    internal static string LinearWriteError(Error error, string team, string key) => error.Code switch
    {
        "SND-LINEAR-002" => LinearWriteAccess,
        "SND-LINEAR-003" => LinearTeamNotPicked(team),
        "SND-LINEAR-004" => "Linear refused the write key. Make a new one with write permission.",
        "SND-LINEAR-005" => $"No issue {key} in your teams (deleted, moved, or not synced yet).",
        "SND-LINEAR-010" => "Couldn't reach Linear, so nothing was changed. Try again.",
        _ => error.Message,
    };
}
