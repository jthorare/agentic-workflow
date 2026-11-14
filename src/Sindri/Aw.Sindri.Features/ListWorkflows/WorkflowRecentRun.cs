using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.ListWorkflows;

/// <summary>One bar of the list row's strip.</summary>
/// <param name="Run">The run.</param>
/// <param name="Status">The run's status word.</param>
/// <param name="StartedAt">When it was recorded.</param>
/// <param name="Seconds">How long it took, or null while it has not ended.</param>
internal sealed record WorkflowRecentRun(WorkflowRunId Run, string Status, DateTimeOffset StartedAt, double? Seconds);
