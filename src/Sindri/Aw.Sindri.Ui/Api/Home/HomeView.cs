namespace Aw.Sindri.Ui.Api.Home;

/// <summary>GET /sindri/home (home spec §8): a page of the rows that need the owner, each group in ranked order.</summary>
/// <param name="AsOf">How fresh the views are.</param>
/// <param name="Counts">How many rows each bucket holds over the whole view.</param>
/// <param name="NeedsYou">The page's needs-you rows.</param>
/// <param name="Moving">The page's moving rows.</param>
/// <param name="Cursor">The id to send as 'after' for the next page, or null at the end.</param>
/// <param name="Degraded">What makes the page less than whole; empty when nothing does.</param>
internal sealed record HomeView(AsOfView AsOf, HomeCountsView Counts, IReadOnlyList<HomeRowView> NeedsYou, IReadOnlyList<HomeRowView> Moving, string? Cursor, IReadOnlyList<HomeDegradedView> Degraded);
