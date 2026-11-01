namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One write: the pages to set (each replaces the page with its id; a null id adds a page) and the page ids to remove.</summary>
/// <param name="Set">The pages to set.</param>
/// <param name="Remove">The page ids to remove.</param>
internal sealed record PageSave(IReadOnlyList<PageDraft> Set, IReadOnlyList<string> Remove)
{
    /// <summary>Gets the key paths that already waited when the sheet read the profile: a new page's path is any '.pages.' path not among them.</summary>
    public IReadOnlyList<string> Before { get; init; } = [];
}
