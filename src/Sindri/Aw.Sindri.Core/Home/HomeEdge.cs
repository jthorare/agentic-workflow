namespace Aw.Sindri.Core.Home;

/// <summary>One edge of the Home link graph the rank reads (home spec §4.1).</summary>
/// <param name="From">The object the edge leaves.</param>
/// <param name="To">The object it reaches.</param>
/// <param name="Edge">How they relate.</param>
public sealed record HomeEdge(string From, string To, HomeEdgeKind Edge);
