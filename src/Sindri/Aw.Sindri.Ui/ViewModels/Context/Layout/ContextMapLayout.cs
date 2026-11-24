using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Layout;

/// <summary>
/// The map's deterministic layout (F3 spec 1 §2, "Layout"): a pure function of the lenses answer. The world is 1360 px wide with
/// three bands; rows are placed by kind, ordered inside a row by the mean x of the nodes each links to (ties by key), and a row
/// that holds more than seven chips wraps 86 px lower, growing the band and every band under it.
/// </summary>
internal static partial class ContextMapLayout
{
    private const double WorldWidth = 1360;
    private const double ChipHeight = 52;
    private const double CardHeight = 124;
    private const double Pitch = 190;
    private const double FirstCentre = 109;
    private const double HalfColumn = 95;
    private const double RowStep = 86;
    private const double RightLimit = 1336;
    private const int Columns = 7;
    private const double IntentHeight = 252;
    private const double ProductHeight = 336;
    private const double CodeHeight = 184;

    /// <summary>Places the nodes, bands and edges of an answer on the world.</summary>
    /// <param name="view">The lenses answer.</param>
    /// <returns>The layout.</returns>
    internal static MapLayout Build(LensesView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var entries = Entries(view);
        var edges = Links(view, entries);
        var places = Place(entries, edges);
        var nodes = entries.Where(e => places.Rects.ContainsKey(e.Key)).Select(e => e.ToLayout(places.Rects[e.Key])).ToList();
        var bands = new List<MapBandLayout>
        {
            new(MapBand.Intent, 0, places.IntentHeight, Labels.ContextMapBandIntent, Labels.ContextMapMetaIntent(view.Bands.Intent.Counts.Records, view.Bands.Intent.Counts.Decisions, view.Conflicts.Count)),
            new(MapBand.Product, places.ProductTop, places.ProductHeight, Labels.ContextMapBandProduct, Labels.ContextMapMetaProduct(view.Anchors.Count, view.Bands.Product.Counts.Affects, view.Bands.Product.Own.Count + view.Bands.Product.Neighbors.Count + view.Bands.Product.Affects.Count)),
            new(MapBand.Code, places.CodeTop, places.CodeHeight, Labels.ContextMapBandCode, Labels.ContextMapMetaCode(view.Bands.Code.Files.Count(f => f.Tests.Count == 0), view.Bands.Code.Files.Count(f => f.Tests.Count > 0), view.Bands.Code.Symbols.State)),
        };
        var laid = Route(edges, nodes);
        return new MapLayout(new MapRect(0, 0, WorldWidth, places.CodeTop + places.CodeHeight), bands, nodes, laid);
    }

    private sealed record Places(IReadOnlyDictionary<string, MapRect> Rects, double IntentHeight, double ProductTop, double ProductHeight, double CodeTop, double CodeHeight);
}
