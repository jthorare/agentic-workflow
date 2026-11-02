namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>A page as the sheet edits it, and as one 'index.pages' entry is written.</summary>
/// <param name="Id">The page id: an owner page's, or the one the map offers a suggested page; null for New page (the server makes one from the name).</param>
/// <param name="Name">The name.</param>
/// <param name="Repo">The repo, or null to let the server judge it.</param>
/// <param name="Surface">The map page's node key it names, or null for a page with no map page.</param>
/// <param name="Owner">True for a page the owner already named (Edit page), false for a suggested one (Name this page) or a new one.</param>
/// <param name="Actions">Its actions.</param>
internal sealed record PageDraft(string? Id, string Name, string? Repo, string? Surface, bool Owner, IReadOnlyList<PageActionDraft> Actions);
