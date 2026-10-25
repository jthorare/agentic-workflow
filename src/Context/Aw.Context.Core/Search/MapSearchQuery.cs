using Aw.Context.Core.Map;

namespace Aw.Context.Core.Search;

/// <summary>A ranked full-text question over one map: the words, how many hits to return, which area and repo, and which map.</summary>
/// <param name="Q">The words to find; the last one also matches as a prefix. Blank is SND-CONTEXT-006.</param>
/// <param name="Limit">How many hits to return, 1 to <see cref="MapSearchRules.MaxLimit"/>, or null for <see cref="MapSearchRules.DefaultLimit"/>.</param>
/// <param name="Area">One area's key to search within, or null for the whole map.</param>
/// <param name="Repo">A human caller's repo when the profile has more than one, or null (a session is always its own repo).</param>
/// <param name="Read">Which map: the current version, the draft or a retained version.</param>
public sealed record MapSearchQuery(string Q, int? Limit, string? Area, string? Repo, MapRead Read);
