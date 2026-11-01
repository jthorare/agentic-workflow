namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The suggested page a block names.</summary>
/// <param name="Key">The surface's key.</param>
/// <param name="Id">The page's id.</param>
/// <param name="Name">Its name on the map.</param>
/// <param name="Actions">How many actions it has.</param>
internal sealed record PagesPreviewMatch(string Key, string Id, string Name, int Actions);
