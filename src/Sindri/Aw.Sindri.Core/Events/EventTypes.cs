using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The event types Sindri records (each one is in EventCatalog).</summary>
public static class EventTypes
{
    /// <summary>The control plane took the lease and started.</summary>
    public const string ProcessStarted = "process.started";

    /// <summary>The control plane released the lease and stopped.</summary>
    public const string ProcessStopped = "process.stopped";

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

    /// <summary>A human approved a profile hash.</summary>
    public const string ProfileApproved = "profile.approved";

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

    /// <summary>An item moved from one state to another.</summary>
    public const string ItemStateChanged = "item.state-changed";

    /// <summary>An existing item was linked to a tracker record.</summary>
    public const string ItemLinked = "item.linked";

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

    /// <summary>The event a background service records per run: 'service.&lt;name&gt;'.</summary>
    /// <param name="service">The service.</param>
    /// <returns>The event type, for example 'service.heartbeat'.</returns>
    public static string ServiceRun(ServiceName service) => "service." + service.Value;
}
