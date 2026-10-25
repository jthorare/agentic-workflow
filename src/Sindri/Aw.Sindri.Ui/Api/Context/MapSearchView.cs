namespace Aw.Sindri.Ui.Api.Context;

/// <summary>GET /sindri/context/search's answer: the hits, best first.</summary>
/// <param name="MapVersion">MapVersion, as served.</param>
/// <param name="Total">How many items matched, of which Hits holds the first Limit.</param>
/// <param name="Hits">The hits, best first.</param>
internal sealed record MapSearchView(string MapVersion, int Total, IReadOnlyList<MapSearchHitView> Hits);
