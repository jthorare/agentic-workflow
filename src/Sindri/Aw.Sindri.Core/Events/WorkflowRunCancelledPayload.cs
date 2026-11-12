using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'workflow.run.cancelled' payload (issue 2258, PR 6): someone cancelled a queued or running run.</summary>
/// <param name="RunId">The run's id.</param>
/// <param name="Reason">Why, or null.</param>
/// <param name="By">Who cancelled it.</param>
/// <param name="Mode">'live' or 'test' (<see cref="WorkflowRunMode"/>). Events written before the field existed read as null, which means live.</param>
public sealed record WorkflowRunCancelledPayload(WorkflowRunId RunId, string? Reason, string By, string Mode);
