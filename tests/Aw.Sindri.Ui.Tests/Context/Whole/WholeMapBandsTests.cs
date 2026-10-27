using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#1096: no node is drawn outside the band of its kind, and no band overlaps another, on the maps the Context page really draws: whole, filtered by a search, opened by feature and drilled into a band.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapBandsTests
{
    private static readonly string[] _goldens = ["nodes-small", "concept-open"];

    private static IEnumerable<WholeMapGeometry> Whole() => _goldens.Select(g => WholeMapLayout.Place(WholeMapLayoutTests.Draft(g)));

    // The overview of the nodes-small fixture with its feature opened in full and the other area left as a tile.
    private static WholeMapGeometry Mixed()
    {
        var full = WholeMapLayoutTests.Draft("nodes-small");
        var open = WholeMapLayoutTests.Draft("area-open");
        return WholeMapLayout.PlaceMixed(full with { Detail = "areas", Nodes = null, Edges = null }, [open]);
    }

    private static string N(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    private static void AssertInBands(WholeMapGeometry geometry, string what)
    {
        foreach (var node in geometry.Nodes)
        {
            var band = geometry.Bands.SingleOrDefault(b => b.Band == NodeBands.Of(node.Kind));
            Assert.True(band is not null, $"{what}: node '{node.Key}' ({node.Kind}) has no {NodeBands.Of(node.Kind)} band to sit in");
            Assert.True(node.Rect.Y >= band.Top && node.Rect.Bottom <= band.Top + band.Height, $"{what}: node '{node.Key}' at y {N(node.Rect.Y)}..{N(node.Rect.Bottom)} is outside its {band.Band} band {N(band.Top)}..{N(band.Top + band.Height)}");
        }

        foreach (var cell in geometry.Tiles.SelectMany(t => t.BandCells))
        {
            var band = geometry.Bands.SingleOrDefault(b => b.Band == cell.Band);
            Assert.True(band is not null, $"{what}: a tile's {cell.Band} cell has no band to sit in");
            Assert.True(cell.Rect.Y >= band.Top && cell.Rect.Bottom <= band.Top + band.Height, $"{what}: a tile's {cell.Band} cell at y {N(cell.Rect.Y)}..{N(cell.Rect.Bottom)} is outside its band {N(band.Top)}..{N(band.Top + band.Height)}");
        }
    }

    private static void AssertNoOverlap(WholeMapGeometry geometry, string what)
    {
        for (var i = 1; i < geometry.Bands.Count; i++)
        {
            var (above, below) = (geometry.Bands[i - 1], geometry.Bands[i]);
            Assert.True(above.Top + above.Height <= below.LaneTop, $"{what}: the {above.Band} band ends at {N(above.Top + above.Height)} but the {below.Band} lane starts at {N(below.LaneTop)}");
        }
    }

    private static void AssertSound(WholeMapGeometry geometry, string what)
    {
        AssertInBands(geometry, what);
        AssertNoOverlap(geometry, what);
    }

    [Fact]
    public void Every_node_of_the_whole_map_sits_inside_its_band_and_the_bands_do_not_overlap()
    {
        foreach (var (geometry, i) in Whole().Select((g, i) => (g, i)))
        {
            AssertSound(geometry, "whole map " + _goldens[i]);
        }
    }

    [Fact]
    public void Every_node_of_a_map_opened_by_feature_sits_inside_its_band_and_so_does_every_tile_cell() => AssertSound(Mixed(), "mixed map");

    [Fact]
    public void Every_node_stays_inside_its_band_when_a_search_keeps_one_node_and_its_links()
    {
        foreach (var (geometry, i) in Whole().Select((g, i) => (g, i)))
        {
            foreach (var node in geometry.Nodes)
            {
                HashSet<string> keep = [node.Key, .. geometry.Edges.Where(e => e.From == node.Key || e.To == node.Key).SelectMany(e => new[] { e.From, e.To })];
                AssertSound(WholeMapFilter.Keep(geometry, keep), $"{_goldens[i]} filtered to '{node.Key}'");
            }
        }
    }

    [Fact]
    public void A_tile_kept_by_a_search_keeps_the_band_its_cells_sit_in()
    {
        var mixed = Mixed();

        foreach (var tile in mixed.Tiles)
        {
            AssertSound(WholeMapFilter.Keep(mixed, new HashSet<string>(StringComparer.Ordinal) { tile.Key }), $"mixed map filtered to the tile '{tile.Key}'");
        }
    }

    [Fact]
    public void Every_node_stays_inside_its_band_when_a_band_is_drilled_into()
    {
        string[] names = ["concept of the drill fixture", .. _goldens];
        var maps = new[] { WholeMapBandDrillTests.Concept() }.Concat(Whole()).ToList();

        foreach (var (geometry, i) in maps.Select((g, i) => (g, i)))
        {
            foreach (var band in Enum.GetValues<WholeBand>())
            {
                AssertSound(WholeMapBandDrill.Cross(geometry, band), $"{names[i]} drilled across {band}");
                AssertSound(WholeMapBandDrill.Own(geometry, band), $"{names[i]} drilled into {band}");
            }
        }
    }
}
