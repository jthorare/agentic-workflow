using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>The answer to one trigger.</summary>
/// <param name="Kind">What happened.</param>
/// <param name="RunId">The run's id (also for a skip: the id the trigger would have had).</param>
public sealed record GraphStart(GraphStartKind Kind, WorkflowRunId RunId);
