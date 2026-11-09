namespace Aw.Sindri.Features.ListWorkflowBlocks;

/// <summary>One group of the picker.</summary>
/// <param name="Group">The group's name.</param>
/// <param name="Blocks">Its blocks, by title.</param>
internal sealed record BlockGroupView(string Group, IReadOnlyList<BlockView> Blocks);
