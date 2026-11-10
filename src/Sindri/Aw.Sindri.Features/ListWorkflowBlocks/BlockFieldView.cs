namespace Aw.Sindri.Features.ListWorkflowBlocks;

/// <summary>One config field of a block.</summary>
/// <param name="Name">The field's name.</param>
/// <param name="Type">'text', 'boolean', 'number', 'text list' or 'object'.</param>
/// <param name="Required">True when the config must have it.</param>
internal sealed record BlockFieldView(string Name, string Type, bool Required);
