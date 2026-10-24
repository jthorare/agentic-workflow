using System.Globalization;
using Aw.Sindri.Ui.ViewModels.Confirmations;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the approve-all dialog (setup-once spec §4, §5.3). Plain words: a recipe is a workflow, an id waits in Details.</summary>
internal static partial class Labels
{
    /// <summary>The dialog's title and the catalog row's label.</summary>
    internal const string ApproveAllTitle = "Approve all workflows";

    /// <summary>While the plan is read.</summary>
    internal const string ApproveReading = "Reading what needs your approval";

    /// <summary>The line under the heading: why the list matters.</summary>
    internal const string ApproveWhy = "Approving lets Sindri run these workflows. Read what each one can do first.";

    /// <summary>The heading over the workflows that need their own approval.</summary>
    internal const string ApproveOwnHeading = "Needs its own look";

    /// <summary>What the own-look list says about itself.</summary>
    internal const string ApproveOwnHint = "These can now do something new, so they are not part of this approval. Approve each one on its own.";

    /// <summary>The hold button while idle: the shortest hold is the server's, shown as served.</summary>
    /// <param name="minHoldMs">The shortest hold in milliseconds.</param>
    /// <returns>"Hold to approve, 1.5 seconds".</returns>
    internal static string ApproveHold(int minHoldMs) => string.Create(CultureInfo.InvariantCulture, $"Hold to approve, {minHoldMs / 1000d:0.#} seconds");

    /// <summary>While the button is held.</summary>
    /// <param name="heldMs">How long it has been held.</param>
    /// <param name="minHoldMs">The shortest hold.</param>
    /// <returns>"Keep holding, 0.8 of 1.5 seconds".</returns>
    internal static string ApproveHolding(int heldMs, int minHoldMs) =>
        string.Create(CultureInfo.InvariantCulture, $"Keep holding, {Math.Min(heldMs, minHoldMs) / 1000d:0.#} of {minHoldMs / 1000d:0.#} seconds");

    /// <summary>Spoken when the hold starts.</summary>
    internal const string ApproveHoldStarted = "Holding. Keep holding to approve.";

    /// <summary>Spoken and shown when the button is let go early.</summary>
    internal const string ApproveHoldAgain = "Not approved, hold again";

    /// <summary>Shown when the challenge ran out while the dialog waited.</summary>
    internal const string ApproveExpired = "That took too long. Asking again: hold once more.";

    /// <summary>The line over the typed alternative.</summary>
    internal const string ApproveTypeCaption = "Or type this text to approve:";

    /// <summary>The typed field's accessible name.</summary>
    internal const string ApproveTypeName = "Type the text to approve";

    /// <summary>The typed answer's button.</summary>
    internal const string ApproveTypeButton = "Approve with the typed text";

    /// <summary>The close button.</summary>
    internal const string ApproveClose = "Close";

    /// <summary>The close button once the approval ended.</summary>
    internal const string ApproveDone = "Done";

    /// <summary>The Details disclosure's heading inside the dialog.</summary>
    internal const string ApproveDetails = "Details";

    /// <summary>The heading over the workflows the approval left.</summary>
    internal const string ApproveNotApproved = "Not approved";

    /// <summary>The heading over the workflows left because one they wait on was not approved.</summary>
    internal const string ApproveWaiting = "Waiting on those";

    /// <summary>Spoken when the plan is ready to approve.</summary>
    internal const string ApproveReady = "Ready to approve. Hold the button or type the text.";

    /// <summary>An agent session asked: only the person at the app may approve.</summary>
    internal const string ApproveSessionRefused = "Only you can approve from the app. An agent session can't.";

    /// <summary>A second approval was asked for while one runs (SND-JOB-409).</summary>
    internal const string ApproveAlreadyRunning = "Approve all is already running.";

    /// <summary>The plan changed between the reading and the answer.</summary>
    internal const string ApprovePlanChanged = "The plan changed while you were reading it. Read it again, then approve.";

    /// <summary>The job's end could not be read.</summary>
    internal const string ApproveLostTrack = "Sindri stopped answering before the approval ended. Check Health, then read the plan again.";

    /// <summary>The job was cancelled.</summary>
    internal const string ApproveCancelled = "The approval was cancelled before it finished.";

    /// <summary>A plan whose only workflows need their own look.</summary>
    internal const string ApproveOnlyOwn = "Nothing here can be approved at once. These need their own look.";

    /// <summary>Nothing waits for approval and the server gave no sentence for it.</summary>
    internal const string ApproveNothingWaiting = "Nothing is waiting for your approval.";

    /// <summary>A fresh confirmation could not be had.</summary>
    internal const string ApproveAskFailed = "Sindri did not give a new confirmation. Try again.";

    /// <summary>The retry button.</summary>
    internal const string ApproveTryAgain = "Try again";

    /// <summary>How many workflows the approval covers.</summary>
    /// <param name="count">The count.</param>
    /// <returns>"3 workflows will be approved".</returns>
    internal static string ApprovePlanHeading(int count) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "workflow" : "workflows")} will be approved");

    /// <summary>The hold button's accessible name once it counts the workflows.</summary>
    /// <param name="count">The count.</param>
    /// <param name="minHoldMs">The shortest hold.</param>
    /// <returns>"Hold to approve, 1.5 seconds, 3 workflows": it starts with the words the button shows.</returns>
    internal static string ApproveHoldName(int count, int minHoldMs) =>
        string.Create(CultureInfo.InvariantCulture, $"{ApproveHold(minHoldMs)}, {count} {(count == 1 ? "workflow" : "workflows")}");

    /// <summary>The typed text did not match.</summary>
    /// <param name="expected">What to type.</param>
    /// <returns>The sentence.</returns>
    internal static string ApproveTypedMismatch(string expected) => "That doesn't match. Type exactly: " + expected;

    /// <summary>Why a workflow is in the plan, by the change the server serves.</summary>
    /// <param name="change">'new', 'updated' or 'second-look'.</param>
    /// <param name="served">The server's word for it, used for a change this build doesn't know.</param>
    /// <returns>The reason.</returns>
    internal static string ApproveReason(string change, string served) => change switch
    {
        "new" => "New: you haven't approved it before",
        "updated" => "Updated since you last approved it",
        "second-look" => "Needs a second look: a part it uses changed",
        _ => served,
    };

    /// <summary>The capabilities a new version added.</summary>
    /// <param name="sentences">What is newly allowed.</param>
    /// <returns>The line.</returns>
    internal static string ApproveNewlyAllowed(string sentences) => "Newly allowed: " + sentences;

    /// <summary>What the dialog says while the approval runs.</summary>
    /// <param name="line">The job's latest line, or empty.</param>
    /// <returns>The words.</returns>
    internal static string ApproveRunning(string line) => line.Length > 0 ? line : "Approving workflows";

    /// <summary>The end of an approval.</summary>
    /// <param name="count">How many were approved.</param>
    /// <returns>"3 workflows approved".</returns>
    internal static string ApproveOutcome(int count) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "workflow" : "workflows")} approved");

    /// <summary>A workflow left because one it waits on was not approved.</summary>
    /// <param name="name">The workflow.</param>
    /// <param name="because">The one it waits on.</param>
    /// <returns>The line.</returns>
    internal static string ApproveSkipped(string name, string because) => $"{name} waits for {because}";

    /// <summary>How to approve a refused workflow on its own.</summary>
    /// <param name="cli">The served command.</param>
    /// <returns>The line.</returns>
    internal static string ApproveOnItsOwn(string cli) => "Approve it on its own: " + cli;

    /// <summary>One plan row's accessible name.</summary>
    /// <param name="name">The workflow.</param>
    /// <param name="reason">Why it is in the plan.</param>
    /// <returns>The name.</returns>
    internal static string ApproveRowName(string name, string reason) => name + ", " + reason;

    /// <summary>The typed confirmation's lead-in, before the phrase.</summary>
    internal const string TypedAsk = "To approve, type";

    /// <summary>The typed confirmation's note: the phrase belongs to exactly what was read.</summary>
    internal const string TypedNote = "The phrase is tied to exactly this list. If your settings change before you press the button, Sindri asks again.";

    /// <summary>The typed confirmation's mark for a match, read aloud in place of the tick glyph.</summary>
    internal const string TypedMatches = "Matches";

    /// <summary>The hold button while the answer is on its way.</summary>
    internal const string HoldApproving = "Approving...";

    /// <summary>The typed confirmation's button, by what it approves.</summary>
    /// <param name="action">ApproveProfile, ApproveRecipe or ApproveAllRecipes.</param>
    /// <returns>The button's words.</returns>
    internal static string TypedButton(string action) => action switch
    {
        "ApproveProfile" => "Approve settings",
        "ApproveRecipe" => "Approve workflow",
        AppConfirm.GrantTokens => "Grant tokens",
        _ => "Approve all workflows",
    };

    /// <summary>The lock button's verb for a confirmation that is terminal copy for now.</summary>
    /// <param name="action">ApproveProfile, ApproveRecipe or ApproveAllRecipes.</param>
    /// <returns>The verb.</returns>
    internal static string TerminalVerb(string action) => action switch
    {
        "ApproveProfile" => "Approve settings",
        "ApproveRecipe" => "Approve workflow",
        AppConfirm.GrantTokens => "Grant tokens",
        _ => "Approve all",
    };

    /// <summary>What a confirmation acts on, for accessible names.</summary>
    /// <param name="action">ApproveProfile, ApproveRecipe or ApproveAllRecipes.</param>
    /// <returns>The subject.</returns>
    internal static string ConfirmSubject(string action) => action switch
    {
        "ApproveProfile" => "settings",
        "ApproveRecipe" => "workflow",
        AppConfirm.GrantTokens => "token grant",
        _ => "workflows",
    };
}
