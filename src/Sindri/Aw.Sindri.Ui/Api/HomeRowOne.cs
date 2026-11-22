using Aw.Sindri.Ui.Api.Home;

namespace Aw.Sindri.Ui.Api;

/// <summary>GET /sindri/home/rows/{id}: one row with its blocks list.</summary>
/// <param name="AsOf">How fresh the views are.</param>
/// <param name="Row">The row.</param>
internal sealed record HomeRowOne(AsOfView AsOf, HomeRowView Row);
