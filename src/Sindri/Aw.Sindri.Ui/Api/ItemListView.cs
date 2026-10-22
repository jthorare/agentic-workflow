namespace Aw.Sindri.Ui.Api;

/// <summary>GET /sindri/items?state= (API spec §3.1 ListItems, P3): the items in one state, newest first as served.</summary>
/// <param name="Items">The items.</param>
/// <param name="Empty">The view's EmptyText sentence, or null.</param>
/// <param name="AsOf">How fresh the view is.</param>
internal sealed record ItemListView(IReadOnlyList<ItemSummaryView> Items, string? Empty, AsOfView AsOf);
