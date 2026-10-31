namespace Aw.Sindri.Ui.Api.Context;

/// <summary>A page as POST /sindri/context/pages takes it in 'set'.</summary>
/// <param name="Id">The page id; null lets the server give the name's slug.</param>
/// <param name="Name">The name.</param>
/// <param name="Repo">The repo; null when none was matched or asked for.</param>
/// <param name="Surface">The map surface key; null for an owner page.</param>
/// <param name="Actions">Its actions.</param>
internal sealed record PagesPreviewPage(string? Id, string Name, string? Repo, string? Surface, IReadOnlyList<PagesPreviewPageAction> Actions);
