using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>A started call.</summary>
/// <param name="Run">The called run's id.</param>
/// <param name="Status">'started', 'queued' or 'skipped'.</param>
public sealed record GraphCalled(WorkflowRunId Run, string Status);
