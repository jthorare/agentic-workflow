namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One write: the pages to set (each replaces the page with its id) and the page ids to remove.</summary>
/// <param name="Set">The pages to set.</param>
/// <param name="Remove">The page ids to remove.</param>
internal sealed record PageSave(IReadOnlyList<PageDraft> Set, IReadOnlyList<string> Remove);
