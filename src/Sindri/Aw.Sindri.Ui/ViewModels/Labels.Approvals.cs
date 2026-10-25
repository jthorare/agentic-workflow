using System.Globalization;

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
    internal const string ApproveReady = "Ready to approve. Press the Approve button.";

    /// <summary>A refused approval as the app shows it: the code, the words, and the row's served command to run instead; never a silent failure.</summary>
    /// <param name="code">The error code; empty for none.</param>
    /// <param name="words">What went wrong, in words.</param>
    /// <param name="cli">The row's served command; empty when it served none.</param>
    /// <returns>The line.</returns>
    internal static string ApproveRefusal(string code, string words, string cli) =>
        (code.Length > 0 ? code + ": " : "") + words + (cli.Length > 0 ? " Run: " + cli : "");

    /// <summary>A step Sindri launched itself answered: Sindri's own steps are refused.</summary>
    internal const string ApproveSessionRefused = "Sindri's own steps can't answer this.";

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

    /// <summary>How many workflows the approval covers.</summary>
    /// <param name="count">The count.</param>
    /// <returns>"3 workflows will be approved".</returns>
    internal static string ApprovePlanHeading(int count) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "workflow" : "workflows")} will be approved");

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

    /// <summary>The approve-all button.</summary>
    internal const string ApproveAllButton = "Approve all workflows";

    /// <summary>The profile approval's button.</summary>
    internal const string ApproveSettings = "Approve settings";

    /// <summary>The Grant tokens control's title and button.</summary>
    internal const string GrantTokens = "Grant tokens";
}
