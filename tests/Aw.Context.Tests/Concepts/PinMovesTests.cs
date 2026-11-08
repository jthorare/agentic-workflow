using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;

namespace Aw.Context.Tests.Concepts;

/// <summary>What a pin gained and lost between two map versions, and where the lost features went (the pins read's 'moved' field).</summary>
public sealed class PinMovesTests
{
    private static Dictionary<string, IReadOnlySet<string>> Members(params (string Pin, string[] Features)[] pins) =>
        pins.ToDictionary(p => p.Pin, p => (IReadOnlySet<string>)p.Features.ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

    private static Dictionary<string, IReadOnlyList<FeaturePlace>> Places(params (string Feature, string[] Concepts)[] features) =>
        features.ToDictionary(f => f.Feature, f => (IReadOnlyList<FeaturePlace>)[.. f.Concepts.Select(c => new FeaturePlace(ConceptId.From(c), c.ToUpperInvariant()))], StringComparer.Ordinal);

    private static PinMove Of(string pin, IReadOnlyDictionary<string, IReadOnlySet<string>> before, IReadOnlyDictionary<string, IReadOnlySet<string>> after, IReadOnlyDictionary<string, IReadOnlyList<FeaturePlace>> places) =>
        PinMoves.Between(41, 42, before, after, places).Single(m => m.Pin.Value == pin);

    [Fact, Trait("Lane", "unit")]
    public void Features_that_came_and_left_are_counted_and_the_versions_are_carried()
    {
        var move = Of("billing", Members(("billing", ["a", "b", "c"])), Members(("billing", ["b", "c", "d", "e"])), Places(("a", ["refunds"])));

        Assert.Equal((41, 42, 2, 1), (move.FromVersion, move.ToVersion, move.Gained, move.Lost));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lost_feature_went_to_the_other_concept_it_belongs_to_now_with_that_concepts_name()
    {
        var move = Of("billing", Members(("billing", ["a"])), Members(("billing", [])), Places(("a", ["refunds"])));

        var to = Assert.Single(move.To);
        Assert.Equal(("refunds", "REFUNDS", 1), (to.Id!.Value.Value, to.Name, to.Features));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lost_feature_that_is_in_no_concept_now_groups_under_a_null_place()
    {
        var move = Of("billing", Members(("billing", ["a", "b"])), Members(("billing", [])), Places(("a", [])));   // b has no entry at all

        var to = Assert.Single(move.To);
        Assert.Equal((null, null, 2), (to.Id, to.Name, to.Features));
    }

    [Fact, Trait("Lane", "unit")]
    public void Places_are_sorted_biggest_first_and_cut_to_two()
    {
        var move = Of("billing", Members(("billing", ["a", "b", "c", "d", "e", "f"])), Members(("billing", [])),
            Places(("a", ["x"]), ("b", ["y"]), ("c", ["y"]), ("d", ["z"]), ("e", ["z"]), ("f", ["z"])));

        Assert.Equal([("z", 3), ("y", 2)], move.To.Select(t => (t.Id!.Value.Value, t.Features)));
        Assert.Equal(6, move.Lost);   // the count is of every lost feature, not of the places shown
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_whose_features_did_not_move_is_all_zeros_with_no_places()
    {
        var move = Of("billing", Members(("billing", ["a", "b"])), Members(("billing", ["a", "b"])), Places());

        Assert.Equal((0, 0, 0), (move.Gained, move.Lost, move.To.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_new_in_the_later_version_gained_all_its_features_and_lost_none()
    {
        var move = Of("refunds", Members(("billing", ["a"])), Members(("billing", ["a"]), ("refunds", ["x", "y"])), Places());

        Assert.Equal((2, 0, 0), (move.Gained, move.Lost, move.To.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_the_later_version_no_longer_has_gets_no_move()
    {
        var moves = PinMoves.Between(41, 42, Members(("billing", ["a"]), ("gone", ["b"])), Members(("billing", ["a"])), Places());

        Assert.Equal(["billing"], moves.Select(m => m.Pin.Value), StringComparer.Ordinal);
    }
}
