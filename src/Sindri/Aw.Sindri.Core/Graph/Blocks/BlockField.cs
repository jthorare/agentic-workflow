namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One field of a block's config.</summary>
/// <param name="Name">The member's name.</param>
/// <param name="Type">Its type.</param>
/// <param name="Required">Whether the config must have it.</param>
public sealed record BlockField(string Name, BlockFieldType Type, bool Required = true);
