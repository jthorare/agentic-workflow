using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ListTriggerEvents;

/// <summary>List the last events a trigger kind saw (GET /sindri/workflows/trigger-events?kind=&amp;connection=, 'sindri wf trigger-events', MCP workflow_trigger_events).</summary>
/// <param name="Kind">The event kind, such as 'github.pr.opened'.</param>
/// <param name="Connection">The connection to look at, or null for all.</param>
internal sealed record ListTriggerEventsRequest(string Kind, string? Connection) : IQuery<ListTriggerEventsResponse>;
