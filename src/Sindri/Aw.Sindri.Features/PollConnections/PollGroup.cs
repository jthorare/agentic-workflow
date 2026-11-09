using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>One workflow's connection_event trigger: the revision it runs, how it handles a run already in flight, and its filter text (or null).</summary>
/// <param name="Workflow">The workflow.</param>
/// <param name="Slug">Its slug.</param>
/// <param name="Revision">The active revision.</param>
/// <param name="Concurrency">The workflow's concurrency mode.</param>
/// <param name="Filter">The trigger's 'filter' condition, or null for every event.</param>
internal sealed record PollTrigger(WorkflowId Workflow, WorkflowSlug Slug, int Revision, GraphConcurrency Concurrency, string? Filter);

/// <summary>Every active trigger that watches one (connection, event kind): the provider is called once for the group and each trigger filters the events itself.</summary>
/// <param name="Connection">The connection.</param>
/// <param name="Kind">The event kind.</param>
/// <param name="Triggers">The triggers.</param>
internal sealed record PollGroup(ConnectionName Connection, EventKind Kind, IReadOnlyList<PollTrigger> Triggers);
