namespace Aw.Sindri.Ui.Api.Context;

/// <summary>GET /sindri/context/pages: the product's pages with their actions, one slice at a time.</summary>
/// <param name="Pages">This slice's pages.</param>
/// <param name="Total">How many pages match, before the slice.</param>
/// <param name="Truncated">True when more pages follow this slice.</param>
/// <param name="MapVersion">The map read, or empty when none was.</param>
internal sealed record PagesView(IReadOnlyList<PageView> Pages, int Total, bool Truncated, string MapVersion);
