namespace Aw.Context.Core.Pages;

/// <summary>'GET /sindri/context/pages': the product's pages with their actions, one slice at a time.</summary>
/// <param name="Pages">This slice's pages: owner pages in profile order, then suggested ones by action count, most first.</param>
/// <param name="Total">How many pages match, before the slice.</param>
/// <param name="Truncated">True when more pages follow this slice.</param>
/// <param name="MapVersion">The map read ('v13'), or empty when none was.</param>
public sealed record PagesView(IReadOnlyList<PageRow> Pages, int Total, bool Truncated, string MapVersion);
