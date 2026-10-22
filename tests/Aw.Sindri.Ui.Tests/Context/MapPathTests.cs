using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The shortest path from a starting point to a node (F3 spec 1 §4, "why it is in the map"): plain C#, no map needed.</summary>
[Lane(TestLane.Unit)]
public sealed class MapPathTests
{
    private static readonly MapLink[] _chain = [new("e1", "a", "b", "part-of"), new("e2", "b", "c", "on"), new("e3", "x", "y", "reads")];

    [Fact]
    public void The_path_is_the_shortest_run_of_hops_from_the_start()
    {
        var route = MapPath.Find(_chain, ["a"], "c");

        Assert.NotNull(route);
        Assert.Equal("a", route.Start);
        Assert.Equal([("b", "part-of"), ("c", "on")], route.Hops.Select(h => (h.Key, h.Type)));
    }

    [Fact]
    public void An_edge_is_walked_in_both_directions()
    {
        var route = MapPath.Find(_chain, ["c"], "a");

        Assert.NotNull(route);
        Assert.Equal(["b", "a"], route.Hops.Select(h => h.Key), StringComparer.Ordinal);
    }

    [Fact]
    public void A_starting_point_has_a_path_of_no_hops()
    {
        var route = MapPath.Find(_chain, ["a"], "a");

        Assert.NotNull(route);
        Assert.Empty(route.Hops);
    }

    [Fact]
    public void A_node_no_start_reaches_has_no_path()
    {
        Assert.Null(MapPath.Find(_chain, ["a"], "y"));
        Assert.Null(MapPath.Find(_chain, [], "a"));
    }

    [Fact]
    public void With_several_starts_the_nearest_one_wins_and_a_tie_goes_to_the_first()
    {
        MapLink[] links = [new("1", "a", "b", "t"), new("2", "b", "c", "t"), new("3", "c", "d", "t"), new("4", "a", "m", "t"), new("5", "d", "m", "t")];

        Assert.Equal("d", MapPath.Find(links, ["a", "d"], "c")!.Start);
        Assert.Equal("a", MapPath.Find(links, ["a", "d"], "m")!.Start);
    }

    [Fact]
    public void The_sentence_names_the_start_and_each_hop_in_plain_words()
    {
        var route = MapPath.Find(_chain, ["a"], "c");

        var sentence = MapPath.Sentence(k => k.ToUpperInvariant(), route);

        Assert.Equal("Starts at A, then B (part of), then C (happens on).", sentence);
    }

    [Fact]
    public void The_sentence_for_a_start_or_an_unreached_node_is_its_own_line()
    {
        Assert.Equal(Labels.ContextMapPathAnchor, MapPath.Sentence(k => k, MapPath.Find(_chain, ["a"], "a")));
        Assert.Equal(Labels.ContextMapPathNone, MapPath.Sentence(k => k, MapPath.Find(_chain, ["a"], "y")));
    }
}
