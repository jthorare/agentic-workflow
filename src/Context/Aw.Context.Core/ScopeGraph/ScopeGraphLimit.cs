namespace Aw.Context.Core.ScopeGraph;

/// <summary>Something the scoping graph does not hold.</summary>
/// <param name="Why">The reason ('cap', 'role-glob', 'edges-outside', 'no-graph').</param>
/// <param name="Not">What is missing, in words.</param>
public sealed record ScopeGraphLimit(string Why, string Not);
