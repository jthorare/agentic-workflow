using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.DeleteWorkflow;

/// <summary>The workflow is archived.</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="ArchivedAt">When it was archived.</param>
internal sealed record DeleteWorkflowResponse(WorkflowSlug Slug, DateTimeOffset ArchivedAt);
