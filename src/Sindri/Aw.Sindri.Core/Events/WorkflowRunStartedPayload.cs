using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'workflow.run.started' payload (issue 2258, PR 6): a graph run was recorded and its Temporal workflow started.</summary>
/// <param name="RunId">The run's id, '&lt;slug&gt;@&lt;revision&gt;:&lt;occurrence&gt;'.</param>
/// <param name="WorkflowId">The workflow.</param>
/// <param name="Revision">The revision the run walks.</param>
/// <param name="Trigger">What started it, as JSON.</param>
/// <param name="By">Who started it, as the envelope's actor reads.</param>
/// <param name="Mode">'live' or 'test' (<see cref="WorkflowRunMode"/>). Events written before the field existed read as null, which means live.</param>
public sealed record WorkflowRunStartedPayload(WorkflowRunId RunId, WorkflowId WorkflowId, int Revision, string Trigger, string By, string Mode);
