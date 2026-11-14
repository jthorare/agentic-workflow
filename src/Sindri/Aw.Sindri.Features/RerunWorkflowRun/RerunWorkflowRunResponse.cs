using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.RerunWorkflowRun;

/// <summary>What a re-run became.</summary>
/// <param name="Run">The new run's id.</param>
/// <param name="Status">'started' or 'queued'.</param>
/// <param name="Source">The run it was started from.</param>
/// <param name="Restored">The steps whose stored output the new run took from the source, in the graph's node order.</param>
/// <param name="Rerun">The steps the new run runs again: the chosen step and every step after it.</param>
internal sealed record RerunWorkflowRunResponse(WorkflowRunId Run, string Status, WorkflowRunId Source, IReadOnlyList<string> Restored, IReadOnlyList<string> Rerun);
