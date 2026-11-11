namespace Aw.Sindri.Core.Events;

/// <summary>
/// The 'workflow.trigger.skipped' payload (#2258, PR 11): a polled event matched a workflow's connection_event trigger but started no run. The delivery id is the run's occurrence,
/// so a repeat of an event that already has its run is a 'duplicate'; a workflow that runs one at a time with a run in flight skips with 'concurrency'.
/// </summary>
/// <param name="Workflow">The workflow's slug.</param>
/// <param name="Connection">The connection name.</param>
/// <param name="Kind">The event kind.</param>
/// <param name="DeliveryKey">The event's delivery id.</param>
/// <param name="Reason">'concurrency' or 'duplicate'.</param>
/// <param name="At">When the trigger was skipped.</param>
public sealed record WorkflowTriggerSkipped(string Workflow, string Connection, string Kind, string DeliveryKey, string Reason, DateTimeOffset At);
