using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;

namespace Aw.Context.Core.Concepts;

/// <summary>
/// The pin-suggest read (concept pins spec §3.6): exactly one of <paramref name="Concept"/>, <paramref name="Search"/> and <paramref name="Lines"/> is set. The Sindri call checks that and the
/// limits (SND-CONTEXT-179); a query with none set answers none of the three.
/// </summary>
/// <param name="Read">Which map: the current version, the draft or a retained version.</param>
/// <param name="Repo">A human caller's repo when the profile has more than one, else null.</param>
/// <param name="Concept">The concept whose terms to count ('concept:billing'), or null.</param>
/// <param name="Search">The text to find in the features' labels and keys, or null.</param>
/// <param name="Limit">The most features a search answers.</param>
/// <param name="Lines">The pasted list to match to concepts, or null.</param>
public sealed record PinSuggestQuery(MapRead Read, string? Repo, NodeKey? Concept, string? Search, int Limit, IReadOnlyList<PinSuggestLine>? Lines);
