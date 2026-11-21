using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The whole-repo overview as area tiles (<see cref="WholeMapViewModel"/>, continued; #3674): a map of more than <see cref="WholeDrillIn.TileOverviewAbove"/> nodes with no area open shows one tile per area, largest first, never nodes or edges.</summary>
internal sealed partial class WholeMapViewModel
{
    private (WholeMapView Source, IReadOnlyList<AreaCard> Cards)? _cards;

    /// <summary>Gets whether the stage is the overview's area tiles.</summary>
    public bool ShowAreaTiles => IsReady && !IsAreaOpen && !HasConcept && !IsZoomed && _hits is null && Answer is { ConceptList: null, Areas.Count: > 0 } answer && answer.Counts.Nodes > WholeDrillIn.TileOverviewAbove;

    /// <summary>Gets whether the canvas tools (zoom, mini-map, legend) show: a list or tiles are not zoomed.</summary>
    public bool ShowStageTools => !ShowAreaTiles && !ShowAreaList;

    /// <summary>Gets the accessible name of the tiles.</summary>
    public static string AreaTilesName => Labels.ContextWholeTilesName;

    /// <summary>Gets the tiles, largest area first; empty when the stage is not the overview's tiles.</summary>
    public IReadOnlyList<AreaCard> AreaTiles
    {
        get
        {
            if (!ShowAreaTiles || Answer is not { } answer)
            {
                return [];
            }

            if (_cards is not { } have || !ReferenceEquals(have.Source, answer))
            {
                _cards = have = (answer, CardsOf(answer.Areas));
            }

            return have.Cards;
        }
    }

    /// <summary>Gets the line over the tiles: "57 areas · 12171 items".</summary>
    public string AreaTilesHead => AreaTiles.Count == 0 ? string.Empty : Labels.ContextWholeTilesHead(AreaTiles.Count, AreaTiles.Sum(t => t.Members));

    /// <summary>A tile was pressed: the page reads that area in full, whatever level the overview was read at.</summary>
    /// <param name="key">The area's key.</param>
    internal void OpenTile(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        AreaOpenRequested?.Invoke(this, new ValueEventArgs<string>(key));
    }

    // Largest first, then by name; the colour is the area's place in the map's own area order, so a tile keeps its colour in the mini-map's and the list's.
    private static IReadOnlyList<AreaCard> CardsOf(IReadOnlyList<WholeArea> areas)
    {
        var total = Math.Max(1, areas.Sum(a => a.Members));
        return [.. areas
            .Select((a, i) => (Area: a, Colour: WholePalette.For(i).Colour))
            .OrderByDescending(a => a.Area.Members)
            .ThenBy(a => a.Area.Label, StringComparer.Ordinal)
            .ThenBy(a => a.Area.Key, StringComparer.Ordinal)
            .Select(a => CardOf(a.Area, a.Colour, total))];
    }

    private static AreaCard CardOf(WholeArea area, int colour, int total)
    {
        var label = area.Unassigned && area.Label.Length == 0 ? Labels.ContextFilesNoFeature : area.Label;
        var counts = Labels.ContextWholeTileCounts(Of(area, "code"), Of(area, "feature"), Of(area, "record") + Of(area, "decision"));
        return new AreaCard(area.Key, label, area.Members, counts, Labels.ContextWholeAreaCardName(label, counts), (double)area.Members / total, colour);
    }

    private static int Of(WholeArea area, string kind) => area.ByKind.TryGetValue(kind, out var count) ? count : 0;
}
