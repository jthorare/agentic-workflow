using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What a <see cref="GraphWorkflow"/> starts with: only the run id. The spec is read by the first activity (<see cref="GraphRunActivities.LoadAsync"/>), so the
/// workflow input stays small and its history holds the spec exactly once, as that activity's recorded result. A revision never changes, so the read is the same on every replay.
/// </summary>
/// <param name="Run">The run's id; also the Temporal workflow id.</param>
internal sealed record GraphWorkflowInput(WorkflowRunId Run);
