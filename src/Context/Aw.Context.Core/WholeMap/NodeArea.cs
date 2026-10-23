namespace Aw.Context.Core.WholeMap;

/// <summary>The area of one node and the other areas it reaches.</summary>
/// <param name="Area">The area it belongs to.</param>
/// <param name="AlsoIn">Up to 5 other areas, most edges first, ties by key.</param>
public sealed record NodeArea(string Area, IReadOnlyList<string> AlsoIn);
