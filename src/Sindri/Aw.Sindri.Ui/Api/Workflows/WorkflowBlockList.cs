namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>GET /sindri/workflows/blocks: the block types, grouped as the picker shows them.</summary>
/// <param name="Groups">The groups, in the order served.</param>
internal sealed record WorkflowBlockList(IReadOnlyList<WorkflowBlockGroup> Groups);
