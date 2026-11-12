namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One field of a block's config.</summary>
/// <param name="Name">The member's name.</param>
/// <param name="Type">Its type.</param>
/// <param name="Required">Whether the config must have it.</param>
/// <param name="Caption">The plain-words label the editor shows, or null to show the name.</param>
/// <param name="Choices">A closed list: the value must be one of these, or null.</param>
/// <param name="Presets">Open shortcuts: any value is allowed, these are one-click fills, or null.</param>
/// <param name="Advanced">True when the editor hides the field until the person asks for it.</param>
public sealed record BlockField(
    string Name,
    BlockFieldType Type,
    bool Required = true,
    string? Caption = null,
    IReadOnlyList<BlockChoice>? Choices = null,
    IReadOnlyList<BlockChoice>? Presets = null,
    bool Advanced = false);
