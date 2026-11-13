using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The event types Sindri records (each one is in EventCatalog).</summary>
public static partial class EventTypes
{
    /// <summary>The control plane took the lease and started.</summary>
    public const string ProcessStarted = "process.started";

    /// <summary>The control plane released the lease and stopped.</summary>
    public const string ProcessStopped = "process.stopped";

    /// <summary>An instance still open when the recipe engine was removed was cancelled, once, with this note (issue 2258 PR 3).</summary>
    public const string WorkflowRetired = "workflow.retired";

    /// <summary>The retire pass finished (issue 2258 PR 3): written once, even when it retired nothing, so a later start reads no ledger page.</summary>
    public const string WorkflowRetireDone = "workflow.retire.done";

    /// <summary>A recipe instance started (mirrored from its RecipeWorkflow; engine spec rev 8.1 §5.6).</summary>
    public const string WorkflowStarted = "workflow.started";

    /// <summary>A recipe instance moved from one state to another (mirrored from its RecipeWorkflow).</summary>
    public const string WorkflowTransition = "workflow.transition";

    /// <summary>A recipe instance refused a trigger (a gate failed closed, a report's findings): the gate results and what followed (engine spec §5.6).</summary>
    public const string WorkflowRefused = "workflow.refused";

    /// <summary>A sleep or downtime shift moved an instance's clocks (engine spec §5.6).</summary>
    public const string WorkflowShifted = "workflow.shifted";

    /// <summary>A takeoverIdle timer fired and notified the human (engine spec §5.6).</summary>
    public const string WorkflowNotified = "workflow.notified";

    /// <summary>A held event expired from an instance's inbox (engine spec §5.4, §5.6).</summary>
    public const string WorkflowExpired = "workflow.expired";

    /// <summary>An instance took or gave up a heavy-job slot (LN-WF3).</summary>
    public const string WorkflowHeavy = "workflow.heavy";

    /// <summary>The Step inputs of a run filled its injection plan: the sections' names, sizes and digests, never their text (Step inputs plan, S4).</summary>
    public const string WorkflowInjection = "workflow.injection";

    /// <summary>A human approved a profile hash (history: profile edits apply on save now, #2258 PR 1).</summary>
    public const string ProfileApproved = "profile.approved";

    /// <summary>The first start without approvals removed the old approved-snapshot folder (#2258 PR 1).</summary>
    public const string ProfileApprovalsRemoved = "profile.approvals.removed";

    /// <summary>A recipe was registered by hash (engine spec §4.1).</summary>
    public const string RecipeRegistered = "recipe.registered";

    /// <summary>A recipe hash was approved, or re-approved with new pins (engine spec §4.1).</summary>
    public const string RecipeApproved = "recipe.approved";

    /// <summary>A recipe hash was revoked, directly or by a cascade (engine spec §4.1).</summary>
    public const string RecipeRevoked = "recipe.revoked";

    /// <summary>The context engine refreshed its snapshot sources (context spec §3; the context-docs-refresh service).</summary>
    public const string ContextSourcesSynced = "context.sources.synced";

    /// <summary>A human imported a map file as the current product map (context spec §3, §8.7; stage S-3a).</summary>
    public const string ContextMapImported = "context.map.imported";

    /// <summary>A review decision was recorded in the draft map (context spec §3).</summary>
    public const string ContextMapReviewed = "context.map.reviewed";

    /// <summary>A draft map became current (context spec §3).</summary>
    public const string ContextMapPromoted = "context.map.promoted";

    /// <summary>A refresh job built, cleaned up and promoted the map with no human step (owner F1, 2026-10-19).</summary>
    public const string ContextMapRefreshed = "context.map.refreshed";

    /// <summary>A refreshed map draft was held until the code index catches up (code index spec 6.4).</summary>
    public const string ContextMapHeld = "context.map.held";

    /// <summary>A map went ahead of the code index wait, or with nothing to wait for (code index spec 6.4); follows 'context.map.promoted'.</summary>
    public const string ContextMapPublished = "context.map.published";

    /// <summary>A hold ended without a promote: a rebuild replaced the draft, or it was already current (code index spec 6.4).</summary>
    public const string ContextMapHoldDropped = "context.map.hold.dropped";

    /// <summary>A code index build published an index (code index spec §8); appended by the job just before it finishes.</summary>
    public const string CodeIndexBuilt = "codeindex.built";

    /// <summary>The owner approved a scope packet (scoping spec §3, SC3); written by the freeze Step.</summary>
    public const string ScopingApproved = "scoping.approved";

    /// <summary>A later station found a surface or behaviour an approved scope packet missed (scoping spec §3, SC3); written by that station.</summary>
    public const string ScopingEscape = "scoping.escape";

    /// <summary>A scoping run's surfaces were written to the draft map (context spec §3; S-4 adds this sixth event).</summary>
    public const string ContextMapProposed = "context.map.proposed";

    /// <summary>The map was proposed from the repo into a draft (context spec §3).</summary>
    public const string ContextMapBuilt = "context.map.built";

    /// <summary>A command attempt reached the chain: its audit row (not public).</summary>
    public const string CommandHandled = "command.handled";

    /// <summary>A background service ran a slot, ok or failed (API spec C8).</summary>
    public const string ServiceRan = "service.ran";

    /// <summary>The profile watch saw the (approved, live) pair change (API spec C8).</summary>
    public const string ProfileChecked = "profile.checked";

    /// <summary>A human asked for the views to be rebuilt (not public).</summary>
    public const string ViewsRebuilt = "views.rebuilt";

    /// <summary>A session appeared, started asking for approval, or went (the session collector; collector spec CO3).</summary>
    public const string FleetChanged = "fleet.changed";

    /// <summary>A human created a work item.</summary>
    public const string ItemCreated = "item.created";

    /// <summary>A handoff (an outside offer or a local script) made an item and left its whole payload (not public).</summary>
    public const string ItemHandoff = "item.handoff";

    /// <summary>An item moved from one state to another.</summary>
    public const string ItemStateChanged = "item.state-changed";

    /// <summary>An existing item was linked to a tracker record.</summary>
    public const string ItemLinked = "item.linked";

    /// <summary>The owner attached a Linear project to the Projects page, or added one by hand.</summary>
    public const string ProjectAttached = "project.attached";

    /// <summary>The owner edited an attached or hand-made project.</summary>
    public const string ProjectChanged = "project.changed";

    /// <summary>The owner removed an attached or hand-made project.</summary>
    public const string ProjectDetached = "project.detached";

    /// <summary>The owner hid or unhid a project they lead.</summary>
    public const string ProjectHidden = "project.hidden";

    /// <summary>The owner wrote or cleared the notes on a Linear project they lead.</summary>
    public const string ProjectLedNoted = "project.led.noted";

    /// <summary>A caller created a Linear issue through Sindri.</summary>
    public const string LinearIssueCreated = "linear.issue.created";

    /// <summary>A caller posted a comment on a Linear issue through Sindri.</summary>
    public const string LinearCommentPosted = "linear.comment.posted";

    /// <summary>A caller changed fields of a Linear issue through Sindri.</summary>
    public const string LinearIssueChanged = "linear.issue.changed";

    /// <summary>A session or a human filed a request; a decision was created with it.</summary>
    public const string RequestFiled = "request.filed";

    /// <summary>A human chose an option of a decision.</summary>
    public const string DecisionRecorded = "decision.recorded";

    /// <summary>A job-class command was accepted and its work handed to the runtime (API P6).</summary>
    public const string JobStarted = "job.started";

    /// <summary>A job reported one progress line (API P6).</summary>
    public const string JobProgress = "job.progress";

    /// <summary>A job's work ended, succeeded or failed (API P6).</summary>
    public const string JobFinished = "job.finished";

    /// <summary>A human or service cancelled a running job (API P6).</summary>
    public const string JobCancelled = "job.cancelled";

    /// <summary>A running job parked on a named signal (setup-once spec §5.6).</summary>
    public const string JobWaiting = "job.waiting";

    /// <summary>A waiting job's signal came (setup-once spec §5.6).</summary>
    public const string JobResumed = "job.resumed";

    /// <summary>An Agents-page op claimed its act id; a one-off start records its stored answer and worktree here (agents spec §5, §15.8).</summary>
    public const string AgentOp = "agent.op";

    /// <summary>A human paused a background service (API P6).</summary>
    public const string ServicePaused = "service.paused";

    /// <summary>A human resumed a paused background service (API P6).</summary>
    public const string ServiceResumed = "service.resumed";

    /// <summary>A person asked to open an item's agent session (API P10a, UI E-1).</summary>
    public const string ItemOpenRequested = "item.open-requested";

    /// <summary>A launch started a session, whatever its viewer did (API P10a).</summary>
    public const string LaunchStarted = "launch.started";

    /// <summary>A launch failed before any session started (API P10a).</summary>
    public const string LaunchFailed = "launch.failed";

    /// <summary>An engine launch found the session cap full and waits for a free slot (sandbox spec §6); 'launch.outcome' ends the wait.</summary>
    public const string LaunchWaiting = "launch.waiting";

    /// <summary>A skill block's session ended a turn without finishing: it asks the owner a question, left no result or an unreadable one (skill flows spec §7). One per turn end.</summary>
    public const string LaunchOwnerWaiting = "launch.owner-waiting";

    /// <summary>The model an engine launch was given, and how it was chosen (model-pick spec §6).</summary>
    public const string ModelPicked = "model.picked";

    /// <summary>A model call of the context engine ended (context model routing spec §7.1); one per call, under a job or not.</summary>
    public const string ContextModelCalled = "context.model.called";

    /// <summary>Just before the pick asks Jev: "choosing model…" until model.picked for the same launch ends it (model-pick spec §6).</summary>
    public const string ModelPicking = "model.picking";

    /// <summary>The launcher probed the viewer, the host, the targets and the repos (API P10a, UI E-3).</summary>
    public const string LaunchProbed = "launch.probed";

    /// <summary>A launched session ended, by exit or lost heartbeat (API P10a).</summary>
    public const string SessionEnded = "session.ended";

    /// <summary>A usage sample of a launched session (not public; API P10a, SCR E-28).</summary>
    public const string SessionUsage = "session.usage";

    /// <summary>The agent's own session id was read and reported (API P10a, launcher L-9).</summary>
    public const string LaunchSessionReported = "launch.session-reported";

    /// <summary>A client attached to a session whose viewer had not confirmed (API P10a, launcher L-10).</summary>
    public const string LaunchViewerAttached = "launch.viewer-attached";

    /// <summary>The 1st, 10th or 100th denial of a (kind, target) in a sandboxed session (sandbox spec §8).</summary>
    public const string SandboxDenied = "sandbox.denied";

    /// <summary>A sandbox limit was reached without ending the session: pids, memory high or disk growth (sandbox spec §8).</summary>
    public const string SandboxLimitHit = "sandbox.limit-hit";

    /// <summary>A caller saved 'review.login' in the live profile (PR review page spec); the actor says who.</summary>
    public const string ReviewLoginSet = "review.login-set";

    /// <summary>A caller allowed a blocked host or home path for a run's repo in the live profile (sandbox spec §3.6); the actor says who.</summary>
    public const string SandboxAllowed = "sandbox.allowed";

    /// <summary>The owner ended network learn mode early, for one repo or every repo (sandbox spec §3.6).</summary>
    public const string SandboxLearnEnded = "sandbox.learn-ended";

    /// <summary>A takeover tried to open the taken-over Step's live session in a viewer (issue 1004).</summary>
    public const string LaunchTakeoverOpened = "launch.takeover-opened";

    /// <summary>A setup step's derived state changed (setup-once spec §7.5).</summary>
    public const string SetupStepChanged = "setup.step.changed";

    /// <summary>A repo was added to the profile from the app or a terminal (setup-once spec §7.5).</summary>
    public const string RepoAdded = "repo.added";

    /// <summary>The approved profile names a repo and its onboarding ran (setup-once spec §7.5).</summary>
    public const string RepoOnboarded = "repo.onboarded";

    /// <summary>Pins were written into the live profile's 'index.concepts' (concept pins spec §3.6).</summary>
    public const string ContextPinWritten = "context.pin.written";

    /// <summary>Pages were written into the live profile's 'index.pages' (pages spec §3.1).</summary>
    public const string ContextPageWritten = "context.page.written";

    /// <summary>A PR finding was sent as a fix task (PR review page spec §8.2 step 4).</summary>
    public const string ReviewFixSent = "review.fix-sent";

    /// <summary>The shipped recipes were registered at start (setup-once spec §7.5).</summary>
    public const string RecipeRegistrationCompleted = "recipe.registration.completed";

    /// <summary>Sindri registered its MCP server in a repo (setup-once spec §7.5).</summary>
    public const string McpRegistered = "mcp.registered";

    /// <summary>The owner agreed that Sindri may register itself and build the map on its own (setup-once spec §7.5).</summary>
    public const string SetupConsentGiven = "setup.consent.given";

    /// <summary>The setup service made and activated hello-sindri, once per box (first-run spec decision 19).</summary>
    public const string SetupFirstWorkflowCreated = "setup.first_workflow.created";

    /// <summary>A connection's token could not be refreshed, so the owner has to sign in again (connections PR 10b). Keyed by the connection and the time; no token in it.</summary>
    public const string ConnectionNeedsSignIn = "connection.needs-sign-in";

    /// <summary>A connection that needed a new sign-in is connected again (connections PR 10b); it clears the connection's needs-you row.</summary>
    public const string ConnectionSignedIn = "connection.signed-in";

    /// <summary>A connection was removed (#2460): its secret and row are gone. The envelope's actor is who removed it. Not public.</summary>
    public const string ConnectionRemoved = "connection.removed";

    /// <summary>The old Linear keychain entry was imported once as a connection on the Connections page (#3364). The key is in no field. Not public.</summary>
    public const string ConnectionImported = "connection.imported";

    /// <summary>A sign-in (or sign-in again) was started for a connection (#2460). The envelope's actor is who signed in; the row keeps its original creator. Not public.</summary>
    public const string ConnectionSignInRequested = "connection.sign-in-requested";

    /// <summary>Facts were deleted from a memory (#2460). The envelope's actor is who forgot them. Not public.</summary>
    public const string MemoryFactsForgotten = "memory.facts.forgotten";

    /// <summary>A poll of a connection for one event kind returned events (#2258, PR 11). Counts only. Not public.</summary>
    public const string ConnectionPolled = "connection.polled";

    /// <summary>A polled event matched a workflow's trigger but started no run: a run is in flight, or the delivery already had its run (#2258, PR 11). Not public.</summary>
    public const string WorkflowTriggerSkipped = "workflow.trigger.skipped";

    /// <summary>A catch-up hit its page cap, so older events were skipped (#2258, PR 11). Not public.</summary>
    public const string CatchUpCapped = "catchup.capped";

    /// <summary>The event a background service records per run: 'service.&lt;name&gt;'.</summary>
    /// <param name="service">The service.</param>
    /// <returns>The event type, for example 'service.heartbeat'.</returns>
    public static string ServiceRun(ServiceName service) => "service." + service.Value;
}
