namespace Aw.Context.Core.Pages;

/// <summary>One action line of a previewed block.</summary>
/// <param name="Name">The action name as cleaned.</param>
/// <param name="Match">The action of the matched page it names, or null.</param>
/// <param name="Concepts">The concepts written, resolved; a matched action with none written keeps its own.</param>
public sealed record PageListActionRow(string Name, PageListActionMatch? Match, IReadOnlyList<PageListConcept> Concepts);
