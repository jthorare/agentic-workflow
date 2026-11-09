using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>How a <see cref="GraphWorkflow"/> ended.</summary>
/// <param name="Status">The run's final status.</param>
internal sealed record GraphWorkflowResult(WorkflowRunStatus Status);
