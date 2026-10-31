namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One action line of a block.</summary>
/// <param name="Name">The action name as cleaned.</param>
/// <param name="Match">The action of the matched page it names; null when it names none.</param>
/// <param name="Concepts">The concepts written, each with the id it resolved to; a matched action with none written carries its own.</param>
internal sealed record PagesPreviewAction(string Name, PagesPreviewActionMatch? Match, IReadOnlyList<PagesPreviewConcept> Concepts);
