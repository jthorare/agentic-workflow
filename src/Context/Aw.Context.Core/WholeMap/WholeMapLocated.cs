namespace Aw.Context.Core.WholeMap;

/// <summary>Where one asked-for key sits.</summary>
/// <param name="Key">The key as asked.</param>
/// <param name="Area">The node's area, or null when the map has no such node.</param>
public sealed record WholeMapLocated(string Key, string? Area);
