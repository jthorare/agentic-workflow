namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>One config field of a block, as GET /sindri/workflows/blocks serves it.</summary>
/// <param name="Name">The field's name.</param>
/// <param name="Type">"text", "boolean", "number", "text list" or "object".</param>
/// <param name="Required">True when the config must have it.</param>
internal sealed record WorkflowBlockField(string Name, string Type, bool Required);
