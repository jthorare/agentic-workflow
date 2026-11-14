using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the read-only Workflows page, a workflow's runs and a run's detail (#2258 PR 17 part 1). Never a served sentence. The page's name is <see cref="Workflows"/>.</summary>
internal static partial class Labels
{
    /// <summary>The page's one-line purpose.</summary>
    internal const string WfPurpose = "Automations Sindri runs. Open one to see its runs and what each step did.";

    /// <summary>The page before its first answer.</summary>
    internal const string WfReading = "Reading workflows…";

    /// <summary>The page when an answer can't be read.</summary>
    internal const string WfUnreadable = "The workflow list can't be read from this Sindri.";

    /// <summary>The page when a read failed: the reason follows.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfFailed(Error error) => "Couldn't read the workflows just now. " + InstanceRefused(error);

    /// <summary>The list when there are none.</summary>
    internal const string WfNone = "No workflows yet.";

    /// <summary>The action beside a failed read.</summary>
    internal const string WfTryAgain = "Try again";

    /// <summary>The list's head: "2 workflows · 1 active".</summary>
    /// <param name="total">Workflows listed.</param>
    /// <param name="active">Workflows with an active revision.</param>
    /// <returns>The words.</returns>
    internal static string WfHead(int total, int active) =>
        string.Create(CultureInfo.InvariantCulture, $"{total} {(total == 1 ? "workflow" : "workflows")} · {active} active");

    /// <summary>A workflow's state: a glyph and a word, never colour alone.</summary>
    /// <param name="active">True when triggers run a revision.</param>
    /// <returns>The glyph and the word.</returns>
    internal static (string Glyph, string Word) WfState(bool active) => active ? ("●", "Active") : ("○", "Off");

    /// <summary>The revision a row names.</summary>
    /// <param name="active">The active revision, or null when the workflow is off.</param>
    /// <param name="latest">The newest revision.</param>
    /// <returns>The words.</returns>
    internal static string WfRevision(int? active, int latest) => active switch
    {
        _ when latest < 2 => string.Empty,   // a first revision is not news: "Revision 1" says nothing the row doesn't
        null => string.Empty,   // no active revision: no revision words, so a new workflow doesn't say "Newest revision 2" for the rename's bump
        { } a when a == latest => string.Create(CultureInfo.InvariantCulture, $"Revision {a}"),
        { } a => string.Create(CultureInfo.InvariantCulture, $"Revision {a} active, {latest} newest"),
    };

    /// <summary>Who changed a workflow and when; "session:abc" stays as served.</summary>
    /// <param name="by">"owner" or "session:&lt;id&gt;".</param>
    /// <param name="age">How long since.</param>
    /// <returns>The words.</returns>
    internal static string WfChangedBy(string by, TimeSpan age) => "Changed by " + WfActor(by) + ", " + (age.TotalSeconds < 10 ? "just now" : Age(age) + " ago");

    /// <summary>A workflow row's accessible name.</summary>
    /// <param name="name">The workflow's name.</param>
    /// <param name="word">The state's word in lower case.</param>
    /// <param name="revision">The revision words in lower case.</param>
    /// <param name="changed">Who changed it, in lower case.</param>
    /// <returns>The words.</returns>
    internal static string WfRowName(string name, string word, string revision, string changed) => string.Join(", ", new[] { name, word, revision, changed }.Where(static p => p.Length > 0));

    /// <summary>The runs list before its first answer.</summary>
    internal const string WfRunsReading = "Reading runs…";

    /// <summary>The runs list when the workflow never ran.</summary>
    internal const string WfRunsNone = "No runs yet.";

    /// <summary>The runs list for a workflow with no active revision (Sindri refuses the list for it).</summary>
    internal const string WfRunsOff = "This workflow is off, so it has no runs.";

    /// <summary>The head of an empty runs list.</summary>
    internal const string WfRunsHeadNone = "No runs";

    /// <summary>The runs list when a read failed: the reason follows.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfRunsFailed(Error error) => "Couldn't read the runs just now. " + InstanceRefused(error);

    /// <summary>The runs list's head: "1 running · 3 runs".</summary>
    /// <param name="running">Runs running.</param>
    /// <param name="total">Runs listed.</param>
    /// <returns>The words.</returns>
    internal static string WfRunsHead(int running, int total) =>
        string.Create(CultureInfo.InvariantCulture, $"{running} running · {total} {(total == 1 ? "run" : "runs")}");

    /// <summary>The line under a workflow's name: which revision, how big, who made it.</summary>
    /// <param name="shown">The revision shown.</param>
    /// <param name="latest">The newest revision.</param>
    /// <param name="nodes">How many steps the graph has.</param>
    /// <param name="createdBy">Who made the workflow, as served: the person's own actor reads "you".</param>
    /// <param name="hasActive">True when a revision is active; without one, or while the workflow has only its first revision (like the list row, #2845), the line says no revision.</param>
    /// <returns>The words.</returns>
    internal static string WfDetail(int shown, int latest, int nodes, string createdBy, bool hasActive) => string.Create(
        CultureInfo.InvariantCulture,
        $"{(hasActive && latest >= 2 ? (shown == latest ? $"Revision {shown} · " : $"Revision {shown} of {latest} · ") : string.Empty)}{nodes} {(nodes == 1 ? "step" : "steps")} · created by {WfActor(createdBy)}");

    /// <summary>A trigger kind as a word ("manual" is "Manual").</summary>
    /// <param name="kind">The kind as served.</param>
    /// <returns>The word.</returns>
    internal static string WfTriggerWord(string kind) => kind.Length == 0 ? kind : char.ToUpperInvariant(kind[0]) + kind[1..];

    /// <summary>A run row's revision.</summary>
    /// <param name="revision">The revision.</param>
    /// <returns>The words.</returns>
    internal static string WfRunRevision(int revision) => string.Create(CultureInfo.InvariantCulture, $"Revision {revision}");

    /// <summary>A run's or a node's status: a glyph and a word. A status this build doesn't know shows as itself, capitalised, so a newer Sindri never fails the read.</summary>
    /// <param name="status">The status as served.</param>
    /// <returns>The glyph and the word.</returns>
    internal static (string Glyph, string Word) WfChip(string status) => status switch
    {
        "starting" => ("◌", "Starting"),
        "queued" => ("◌", "Queued"),
        "running" => ("●", "Running"),
        "waiting" => ("◐", "Waiting"),
        "succeeded" => ("✓", "Succeeded"),
        "failed" => ("✕", "Failed"),
        "skipped" => ("–", "Skipped"),
        "cancelled" => ("○", "Cancelled"),
        _ => ("○", status.Length == 0 ? status : char.ToUpperInvariant(status[0]) + status[1..]),
    };

    /// <summary>A run row's accessible name.</summary>
    /// <param name="id">The run.</param>
    /// <param name="word">The status word in lower case.</param>
    /// <param name="trigger">The trigger word in lower case.</param>
    /// <param name="when">When.</param>
    /// <param name="error">Why it failed, or empty.</param>
    /// <returns>The words.</returns>
    internal static string WfRunRowName(string id, string word, string trigger, string when, string error) =>
        string.Join(", ", new[] { id, word, trigger, when, error }.Where(p => p.Length > 0));

    /// <summary>The run page before its first answer.</summary>
    internal const string WfRunReading = "Reading the run…";

    /// <summary>The run page when a read failed: the reason follows.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfRunFailed(Error error) => "Couldn't read the run just now. " + InstanceRefused(error);

    /// <summary>The run's title.</summary>
    /// <param name="id">The run.</param>
    /// <returns>The words.</returns>
    internal static string WfRunTitle(string id) => "Run " + id;

    /// <summary>What started a run.</summary>
    /// <param name="kind">The trigger's kind, as served.</param>
    /// <returns>The words.</returns>
    internal static string WfStartedBy(string kind) => "Started by " + kind;

    /// <summary>A run row's trigger when the object has no kind.</summary>
    internal const string WfUnknownTrigger = "an unknown trigger";

    /// <summary>The heading over a run's outputs.</summary>
    internal const string WfOutputsHeading = "What the run produced";

    /// <summary>The heading over a node's input.</summary>
    internal const string WfInputHeading = "Input";

    /// <summary>The heading over a node's output.</summary>
    internal const string WfOutputHeading = "Output";

    /// <summary>The note on a node whose input or output Sindri cut.</summary>
    internal const string WfTruncated = "Cut to fit Sindri's size limit.";

    /// <summary>A for_each node's header.</summary>
    /// <param name="node">The node.</param>
    /// <param name="count">How many items it ran for.</param>
    /// <returns>The words.</returns>
    internal static string WfNodeItems(string node, int count) => string.Create(CultureInfo.InvariantCulture, $"{node} · {count} items");

    /// <summary>One item of a for_each node.</summary>
    /// <param name="index">The item's position, from 0.</param>
    /// <param name="count">How many items.</param>
    /// <returns>The words.</returns>
    internal static string WfItemTitle(int index, int count) => string.Create(CultureInfo.InvariantCulture, $"Item {index + 1} of {count}");

    /// <summary>A node run's accessible name.</summary>
    /// <param name="title">The node or item.</param>
    /// <param name="word">The status word in lower case.</param>
    /// <param name="error">Why it failed, or empty.</param>
    /// <returns>The words.</returns>
    internal static string WfNodeRunName(string title, string word, string error) => string.Join(", ", new[] { title, word, error }.Where(p => p.Length > 0));

    /// <summary>The button on a workflow's runs that starts a run by hand.</summary>
    internal const string WfRunNow = "Run now";

    /// <summary>A Starting placeholder that got no run row for 10 s.</summary>
    internal const string WfRunDidNotStart = "The run didn't start.";

    /// <summary>The Retry button on that row.</summary>
    internal const string WfRunStartRetry = "Retry";

    /// <summary>The same button while its call is on the way.</summary>
    internal const string WfRunNowBusy = "Starting…";

    /// <summary>The runs level's note when Run now was refused or failed: the reason follows.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfRunNowRefused(Error error) => "Couldn't start a run. " + InstanceRefused(error);

    /// <summary>The runs level's note when Run now got no answer from Sindri: the run may have started, so the list is read again.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfRunNowUnsure(Error error) => "Sindri didn't answer, so the run may have started; check the list before you press Run now again. " + InstanceRefused(error);

    /// <summary>The note when a manual run was queued behind another.</summary>
    /// <param name="reason">Why, as served, or null.</param>
    /// <returns>The words.</returns>
    internal static string WfRunQueued(string? reason) => string.IsNullOrEmpty(reason) ? "The run was queued." : "The run was queued: " + reason;

    /// <summary>The note when a manual run was skipped.</summary>
    /// <param name="reason">Why, as served, or null.</param>
    /// <returns>The words.</returns>
    internal static string WfRunSkipped(string? reason) => string.IsNullOrEmpty(reason) ? "The run was skipped." : "The run was skipped: " + reason;

    /// <summary>The button on a live run that asks it to stop.</summary>
    internal const string WfCancelRun = "Cancel run";

    /// <summary>The same button while its call is on the way.</summary>
    internal const string WfCancelRunBusy = "Cancelling…";

    /// <summary>The note after a cancel was delivered.</summary>
    internal const string WfCancelSent = "Cancel sent. The run ends as cancelled when its workflow stops.";

    /// <summary>The note when the run had already ended by the time the cancel arrived.</summary>
    internal const string WfCancelEnded = "That run already ended, so there was nothing to cancel.";

    /// <summary>The note when a cancel was refused or failed: the reason follows.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfCancelRefused(Error error) => "Couldn't cancel the run. " + InstanceRefused(error);

    /// <summary>The button on a workflow that is off: turns its newest revision on.</summary>
    internal const string WfActivate = "Activate";

    /// <summary>The same button while its call is on the way.</summary>
    internal const string WfActivateBusy = "Activating…";

    /// <summary>The button on a workflow that is on: turns it off. Its revisions stay saved.</summary>
    internal const string WfPause = "Pause";

    /// <summary>The same button while its call is on the way.</summary>
    internal const string WfPauseBusy = "Pausing…";

    /// <summary>The note when Activate was refused or failed: the reason follows.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfActivateRefused(Error error) => "Couldn't activate the workflow. " + InstanceRefused(error);

    /// <summary>The note when Pause was refused or failed: the reason follows.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfPauseRefused(Error error) => "Couldn't pause the workflow. " + InstanceRefused(error);

    /// <summary>The note when Activate or Pause got no answer from Sindri: it may have applied, so the list is read again.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfStateUnsure(Error error) => "Sindri didn't answer, so the change may have applied; check the state before you press again. " + InstanceRefused(error);

    /// <summary>The editor's command that reads the workflow again after it changed elsewhere.</summary>
    internal const string WfReload = "Reload";

    /// <summary>The editor's prompt when the workflow changed elsewhere: the edit was not saved and nothing is retried.</summary>
    internal const string WfEditChangedElsewhere = "This workflow changed elsewhere, so your edit was not saved. Reload to see the new version, then make the edit again.";

    /// <summary>The note on the run Run now opened instead of starting a second one.</summary>
    internal const string WfRunInFlight = "A run is already in flight.";

    /// <summary>Under a notification step's title and message on the run page: where it went.</summary>
    internal const string WfNotifySentTo = "Sent to the Sindri inbox";

    /// <summary>The button on a failed step's card on the run page.</summary>
    internal const string WfEditStep = "Edit step";

    /// <summary>The editor's note when an edit was refused or failed: the reason follows.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfEditRefused(Error error) => "Couldn't save the edit. " + InstanceRefused(error);

    /// <summary>The editor's note when Sindri did not answer an edit: it may have been saved.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfEditUnsure(Error error) => "Sindri didn't answer, so the edit may have been saved. Reload to check before you edit again. " + InstanceRefused(error);

    /// <summary>The editor's note when the workflow or the block list can't be read: the reason follows.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfEditLoadFailed(Error error) => "Couldn't read the workflow to edit. " + InstanceRefused(error);

    /// <summary>The editor's note when an edit was saved but the workflow can't be read again: the reason follows.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfEditSavedReloadFailed(Error error) => "The edit was saved, but the workflow couldn't be read again. Reload to see it. " + InstanceRefused(error);

    /// <summary>The line under a required field left empty.</summary>
    internal const string WfFieldNeeded = "This field is required.";

    /// <summary>The line under a number field that isn't a number.</summary>
    internal const string WfFieldNotNumber = "Enter a number.";

    /// <summary>The line under a boolean field that isn't true or false.</summary>
    internal const string WfFieldNotBoolean = "Enter true or false.";

    /// <summary>The line under an object field that isn't a JSON object.</summary>
    internal const string WfFieldNotObject = "Enter a JSON object, such as {\"key\": \"value\"}.";
}
