using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;

namespace Aw.Context.Core.ScopeGraph;

/// <summary>A scoping graph question (issue 1005): the prefiltered subgraph the scoping agent Steps work from, instead of the raw lens dump.</summary>
/// <param name="Anchors">Node keys, aliases or labels; duplicates count once. A concept key (<c lang="text">concept:&lt;slug&gt;</c>) expands to its member features.</param>
/// <param name="Read">Which map to read.</param>
/// <param name="Range">The depth and the cascade hops.</param>
/// <param name="Brief">The brief's text, used only to rank nodes by relevance, or null to rank by structure alone.</param>
/// <param name="MaxNodes">The most nodes the answer may hold; the lowest ranked are dropped, each cut recorded as a limit.</param>
/// <param name="MaxBytes">The most bytes the answer may be, as JSON.</param>
public sealed record ScopeGraphQuery(IReadOnlyList<string> Anchors, MapRead Read, LensRange Range, string? Brief, int MaxNodes, int MaxBytes);
