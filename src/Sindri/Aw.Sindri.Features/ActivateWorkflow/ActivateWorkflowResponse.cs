using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.ActivateWorkflow;

/// <summary>The workflow is on.</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Revision">The revision its triggers run.</param>
/// <param name="SpecHash">That revision's hash.</param>
internal sealed record ActivateWorkflowResponse(WorkflowSlug Slug, int Revision, string SpecHash);
