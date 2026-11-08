using Aw.Meta.Core.Routing;

namespace Aw.Meta.Features.Routing;

/// <summary>What routing found.</summary>
/// <param name="Suggestions">The switches suggested.</param>
/// <param name="Skipped">The lanes running out that cannot move, with the reason.</param>
/// <param name="Applied">Whether the suggestions were written to the team config.</param>
/// <param name="Attention">Whether a suggestion is waiting for the meta to act on it.</param>
public sealed record RouteResponse(IReadOnlyList<RouteSuggestion> Suggestions, IReadOnlyList<RouteSkip> Skipped, bool Applied, bool Attention);
