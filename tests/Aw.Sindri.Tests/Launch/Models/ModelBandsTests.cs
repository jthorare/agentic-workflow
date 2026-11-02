using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch.Models;

/// <summary>The order and the three bands of an agent's models (model-pick spec §3, "Tiers"); pure.</summary>
[Lane(TestLane.Unit)]
public sealed class ModelBandsTests
{
    private const ModelTier C = ModelTier.Cheap;
    private const ModelTier S = ModelTier.Standard;
    private const ModelTier T = ModelTier.Strong;

    private static ModelFacts Model(string id, int? order) => new(id, id, ModelSource.Catalog, order);

    private static string Shown(IReadOnlyList<RankedModel> ranked) =>
        string.Join(' ', ranked.Select(r => $"{r.Model.Slug}:{r.Tiers}{(r.Unordered ? ":unordered" : string.Empty)}"));

    public static TheoryData<int, ModelTier[]> Bands => new()
    {
        { 1, [C | S | T] },
        { 2, [C | S, S | T] },
        { 3, [C, S, T] },
        { 4, [C | S, C | S, S | T, S | T] },
        { 7, [C, C, C, S, T, T, T] },
    };

    [Theory]
    [MemberData(nameof(Bands))]
    public void N_models_are_cut_into_thirds_with_standard_the_middle_or_everything(int count, ModelTier[] expected)
    {
        var models = Enumerable.Range(0, count).Select(i => Model(string.Create(CultureInfo.InvariantCulture, $"m{i}"), i)).ToList();

        var ranked = ModelBands.Rank(models);

        Expect.Equal(string.Join(' ', expected.Select((tiers, i) => string.Create(CultureInfo.InvariantCulture, $"m{i}:{tiers}"))), Shown(ranked));
    }

    [Fact]
    public void Models_are_put_cheapest_first_whatever_order_they_are_given_in()
    {
        var ranked = ModelBands.Rank([Model("opus", 2), Model("haiku", 0), Model("sonnet", 1)]);

        Expect.Equal("haiku:Cheap sonnet:Standard opus:Strong", Shown(ranked));
    }

    [Fact]
    public void A_model_with_no_place_goes_last_in_the_order_given_and_is_marked_unordered()
    {
        var ranked = ModelBands.Rank([Model("mystery-b", order: null), Model("sonnet", 1), Model("mystery-a", order: null), Model("haiku", 0)]);

        Expect.Equal("haiku:Cheap, Standard sonnet:Cheap, Standard mystery-b:Standard, Strong:unordered mystery-a:Standard, Strong:unordered", Shown(ranked));
    }

    [Fact]
    public void Models_with_the_same_place_keep_the_order_given()
    {
        var ranked = ModelBands.Rank([Model("b", 0), Model("a", 0)]);

        Expect.Equal("b:Cheap, Standard a:Standard, Strong", Shown(ranked));
    }

    [Fact]
    public void No_models_rank_to_nothing() => Expect.Equal(0, ModelBands.Rank([]).Count);
}
