using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ListWorkflows;

/// <summary>List the workflows that are not archived (GET /sindri/workflows, 'sindri wf list', MCP workflow_list); with <see cref="IncludeArchived"/>, the archived ones too, each marked.</summary>
internal sealed record ListWorkflowsRequest : IQuery<ListWorkflowsResponse>
{
    /// <summary>True to list archived workflows as well (GET /sindri/workflows?includeArchived=true): an archived slug stays reserved, so a client checking a new slug needs them. Default false.</summary>
    public bool IncludeArchived { get; init; }
}
