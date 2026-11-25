using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What a 'workflow_result' node reads of a finished run.</summary>
/// <param name="Run">The run's id.</param>
/// <param name="Status">'succeeded', 'failed' or 'cancelled'.</param>
/// <param name="FinishedAt">When the run reached its final status, or null when the store has no time for it.</param>
/// <param name="Outputs">The run's outputs (an object keyed by node id), or null.</param>
public sealed record GraphRunResult(WorkflowRunId Run, string Status, DateTimeOffset? FinishedAt, JsonNode? Outputs);
