using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The end of a run, as the walk reports it.</summary>
/// <param name="Run">The run.</param>
/// <param name="Status">Succeeded, Failed or Cancelled.</param>
/// <param name="Error">Why it failed, or null.</param>
internal sealed record GraphFinish(WorkflowRunId Run, WorkflowRunStatus Status, string? Error);
