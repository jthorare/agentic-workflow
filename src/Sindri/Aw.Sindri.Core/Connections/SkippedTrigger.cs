
namespace Aw.Sindri.Core.Connections;

/// <summary>A polled event that started no run, for 'workflow.trigger.skipped'.</summary>
/// <param name="Workflow">The workflow's slug.</param>
/// <param name="Name">The connection.</param>
/// <param name="Kind">The event kind's key (EventKind.Key), so this namespace never depends on the polling one.</param>
/// <param name="DeliveryKey">The event's delivery id.</param>
/// <param name="Reason">'concurrency' or 'duplicate'.</param>
public sealed record SkippedTrigger(string Workflow, ConnectionName Name, string Kind, string DeliveryKey, string Reason);
