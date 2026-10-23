namespace Aw.Context.Core.WholeMap;

/// <summary>The area an answer opened on its own (Part D, W2).</summary>
/// <param name="Key">The area's key.</param>
/// <param name="Truncated">True when the area has more nodes than the cap, so the answer holds the first ones.</param>
/// <param name="Counts">The area's full counts.</param>
public sealed record WholeMapAreaInfo(string Key, bool Truncated, WholeMapCounts Counts);
