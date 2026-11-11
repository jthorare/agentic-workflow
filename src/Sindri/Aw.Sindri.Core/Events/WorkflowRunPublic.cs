using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The public form of 'workflow.run.started', '.finished' and '.cancelled' (issue 2823): the run only.</summary>
/// <param name="RunId">The run.</param>
public sealed record WorkflowRunPublic(WorkflowRunId RunId);
