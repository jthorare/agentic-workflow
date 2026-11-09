namespace Aw.Sindri.Features.ListWorkflowBlocks;

/// <summary>The block types, grouped as the picker shows them.</summary>
/// <param name="Groups">'Triggers', 'Data', 'AI', 'Logic' and 'Outputs' in that order (those with a block), then any other group by name.</param>
internal sealed record ListWorkflowBlocksResponse(IReadOnlyList<BlockGroupView> Groups);
