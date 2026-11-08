namespace Aw.Meta.Core.Routing;

/// <summary>What the planner found.</summary>
/// <param name="Suggestions">The switches to make.</param>
/// <param name="Skipped">The lanes that are running out but cannot move.</param>
public sealed record RoutePlan(IReadOnlyList<RouteSuggestion> Suggestions, IReadOnlyList<RouteSkip> Skipped);
