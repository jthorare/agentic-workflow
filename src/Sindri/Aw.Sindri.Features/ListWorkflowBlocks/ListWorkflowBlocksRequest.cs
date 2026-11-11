using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ListWorkflowBlocks;

/// <summary>List the block types a workflow can use (GET /sindri/workflows/blocks, 'sindri wf blocks', MCP workflow_blocks).</summary>
internal sealed record ListWorkflowBlocksRequest : IQuery<ListWorkflowBlocksResponse>;
