using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.CreateWorkflow;

/// <summary>The new workflow, off, at revision 1.</summary>
/// <param name="Id">The workflow's id.</param>
/// <param name="Slug">Its slug.</param>
/// <param name="Revision">1.</param>
/// <param name="SpecHash">The revision's hash.</param>
internal sealed record CreateWorkflowResponse(WorkflowId Id, WorkflowSlug Slug, int Revision, string SpecHash);
