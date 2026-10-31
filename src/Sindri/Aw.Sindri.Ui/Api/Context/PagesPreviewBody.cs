namespace Aw.Sindri.Ui.Api.Context;

/// <summary>POST /sindri/context/pages/preview's body: the paste the "Add several pages" sheet reads.</summary>
/// <param name="Text">The paste (1 to 65,536 UTF-8 bytes).</param>
/// <param name="Repo">Reads this repo's pages only and fills it into new pages; null for the profile's only repo or every repo.</param>
internal sealed record PagesPreviewBody(string Text, string? Repo = null);
