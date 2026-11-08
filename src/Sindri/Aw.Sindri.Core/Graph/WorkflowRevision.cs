using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph;

/// <summary>One immutable saved version of a workflow.</summary>
/// <param name="WorkflowId">The workflow.</param>
/// <param name="Revision">Its number, from 1.</param>
/// <param name="Spec">The graph.</param>
/// <param name="SpecHash">The graph's hash (<see cref="GraphJson.Hash"/>).</param>
/// <param name="CreatedBy">'owner' or 'session:&lt;id&gt;'.</param>
/// <param name="CreatedAt">When it was saved.</param>
public sealed record WorkflowRevision(WorkflowId WorkflowId, int Revision, GraphSpec Spec, string SpecHash, string CreatedBy, DateTimeOffset CreatedAt);
