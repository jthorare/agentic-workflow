namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>One group of the block picker ("Triggers", "Data", "AI", "Logic", "Outputs" or another name).</summary>
/// <param name="Group">The group's name.</param>
/// <param name="Blocks">Its blocks, by title.</param>
internal sealed record WorkflowBlockGroup(string Group, IReadOnlyList<WorkflowBlock> Blocks);
