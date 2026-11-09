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
