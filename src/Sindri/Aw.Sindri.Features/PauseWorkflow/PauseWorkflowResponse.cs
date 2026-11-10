using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.PauseWorkflow;

/// <summary>The workflow is off.</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="LatestRevision">Its newest revision, which stays saved.</param>
internal sealed record PauseWorkflowResponse(WorkflowSlug Slug, int LatestRevision);
