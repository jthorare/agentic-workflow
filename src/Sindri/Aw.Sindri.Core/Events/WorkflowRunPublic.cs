using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The public form of 'workflow.run.started', '.finished' and '.cancelled' (issue 2823): the run only.</summary>
/// <param name="RunId">The run.</param>
/// <param name="Status">The run's end state ('succeeded' or 'failed') on 'workflow.run.finished'; null on 'started' and 'cancelled', where the event type says it.</param>
/// <param name="Mode">'live' or 'test'; an event written before the field existed reads as 'live'.</param>
public sealed record WorkflowRunPublic(WorkflowRunId RunId, string? Status = null, string Mode = "live");
