using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The public form of the workflow lifecycle events (issue 2823): which workflow changed and its revision. The spec hash and the author stay in the ledger.</summary>
/// <param name="WorkflowId">The workflow.</param>
/// <param name="Slug">Its slug.</param>
/// <param name="Revision">The revision after the change.</param>
public sealed record WorkflowChangedPublic(WorkflowId WorkflowId, WorkflowSlug Slug, int Revision);
