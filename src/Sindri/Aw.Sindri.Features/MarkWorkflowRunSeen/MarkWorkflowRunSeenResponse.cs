using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.MarkWorkflowRunSeen;

/// <summary>What the mark did.</summary>
/// <param name="Run">The run.</param>
/// <param name="Marked">True when the run is a failed live run and is now marked seen; false when there was nothing to mark (a run that did not fail, or a test run).</param>
internal sealed record MarkWorkflowRunSeenResponse(WorkflowRunId Run, bool Marked);
