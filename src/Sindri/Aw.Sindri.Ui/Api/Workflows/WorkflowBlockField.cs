namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>One config field of a block, as GET /sindri/workflows/blocks serves it.</summary>
/// <param name="Name">The field's name.</param>
/// <param name="Type">"text", "boolean", "number", "text list" or "object".</param>
/// <param name="Required">True when the config must have it.</param>
/// <param name="Caption">The plain-words label ("Runs at"); empty when an older Sindri did not send one.</param>
/// <param name="Choices">A closed list of values, or null.</param>
/// <param name="Presets">Open one-click shortcuts (the field still takes any text), or null.</param>
internal sealed record WorkflowBlockField(
    string Name,
    string Type,
    bool Required,
    string Caption = "",
    IReadOnlyList<WorkflowBlockChoice>? Choices = null,
    IReadOnlyList<WorkflowBlockChoice>? Presets = null);
