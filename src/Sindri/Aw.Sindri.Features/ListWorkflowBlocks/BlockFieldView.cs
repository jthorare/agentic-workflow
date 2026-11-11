using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.ListWorkflowBlocks;

/// <summary>One config field of a block.</summary>
/// <param name="Name">The field's name.</param>
/// <param name="Type">'text', 'boolean', 'number', 'text list' or 'object'.</param>
/// <param name="Required">True when the config must have it.</param>
/// <param name="Caption">The plain-words label, always set.</param>
/// <param name="Choices">A closed list of values, or null (left out of the JSON).</param>
/// <param name="Presets">Open one-click shortcuts, or null (left out of the JSON).</param>
/// <param name="Advanced">True when the editor hides the field until asked.</param>
internal sealed record BlockFieldView(
    string Name,
    string Type,
    bool Required,
    string Caption,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<BlockChoiceView>? Choices = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<BlockChoiceView>? Presets = null,
    bool Advanced = false);
