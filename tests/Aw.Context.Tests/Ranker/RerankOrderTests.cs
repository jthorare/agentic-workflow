using Aw.Context.Core.Ranking;

namespace Aw.Context.Tests.Ranker;

public sealed class RerankOrderTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("[2,1]", 2, "1,0")]
    [InlineData("[3,1]", 3, "2,0,1")]
    [InlineData("[1,1,2]", 2, "0,1")]
    [InlineData("[9,1]", 2, "0,1")]
    [InlineData("[2]", 3, "1,0,2")]
    [InlineData("Here you go:\n```json\n[2, 1]\n```", 2, "1,0")]
    public void A_json_array_of_numbers_gives_the_order_with_the_left_out_items_after(string answer, int asked, string expected) =>
        Assert.Equal(expected, string.Join(',', RerankOrder.Parse(answer, asked)!));

    [Theory, Trait("Lane", "unit")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("[0,7]")]
    [InlineData("[\"2\",\"1\"]")]
    [InlineData("{\"order\":[2,1]}")]
    [InlineData("")]
    public void An_answer_that_names_none_of_the_items_is_no_order(string answer) => Assert.Null(RerankOrder.Parse(answer, 2));

    [Fact, Trait("Lane", "unit")]
    public void The_first_items_take_the_models_order_and_the_rest_stay()
    {
        var items = FeatureRankerTests.Fused("a", "b", "c", "d");

        var ordered = RerankOrder.Apply(items, top: 3, [2, 0, 1]);

        Assert.Equal(["repo:c", "repo:a", "repo:b", "repo:d"], ordered.Select(o => o.Key.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_default_list_is_the_top_30() => Assert.Equal(30, RerankOrder.DefaultTop);
}
