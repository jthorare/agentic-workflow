using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ListWorkflows;

/// <summary>List the workflows that are not archived (GET /sindri/workflows, 'sindri wf list', MCP workflow_list).</summary>
internal sealed record ListWorkflowsRequest : IQuery<ListWorkflowsResponse>;
