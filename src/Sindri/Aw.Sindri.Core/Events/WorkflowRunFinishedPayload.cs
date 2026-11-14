using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'workflow.run.finished' payload (issue 2258, PR 6): a run ended on its own.</summary>
/// <param name="RunId">The run's id.</param>
/// <param name="Status">'succeeded' or 'failed'.</param>
/// <param name="Error">Why it failed, or null.</param>
/// <param name="By">Who recorded it.</param>
/// <param name="Mode">'live' or 'test' (<see cref="WorkflowRunMode"/>). Events written before the field existed read as null, which means live.</param>
public sealed record WorkflowRunFinishedPayload(WorkflowRunId RunId, string Status, string? Error, string By, string Mode);
