using Aw.Context.Core.Concepts;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// What the "Pin this" and "Add several" forms offer (GET /sindri/context/pins/suggest, concept pins spec §3.6): exactly one of <paramref name="Concept"/>, <paramref name="Search"/> and <paramref name="Line"/>.
/// No mode or several, an empty search, more than 100 lines, a line over 80 characters with its definition, or a definition count that is neither 0 nor the line count is SND-CONTEXT-179.
/// </summary>
/// <param name="Concept">The concept whose terms to count ('concept:billing'), or null.</param>
/// <param name="Search">The text to find in the features' labels and keys, or null.</param>
/// <param name="Line">The names of a pasted list, one value per line, or null.</param>
/// <param name="Def">The definition words of each line, by index: none, or one per line.</param>
/// <param name="Limit">The most features a search answers (default 50, at most 200).</param>
/// <param name="Repo">A human caller's repo when the profile has more than one, or null.</param>
public sealed record GetContextPinSuggestRequest(string? Concept = null, string? Search = null, IReadOnlyList<string>? Line = null, IReadOnlyList<string>? Def = null, int? Limit = null, string? Repo = null) : IQuery<PinSuggestView>;
