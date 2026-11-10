using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.ReplaceWorkflow;

/// <summary>The revision the replace made.</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Revision">The new revision.</param>
/// <param name="SpecHash">Its hash.</param>
internal sealed record ReplaceWorkflowResponse(WorkflowSlug Slug, int Revision, string SpecHash);
