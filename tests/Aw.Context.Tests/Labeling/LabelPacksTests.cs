using Aw.Context.Core.Models;
using Aw.Context.Infrastructure.Labeling;

namespace Aw.Context.Tests.Labeling;

[Trait("Lane", "unit")]
public sealed class LabelPacksTests
{
    // One question of exactly 1,000 characters: the text and its options (spec 6.3).
    private static ChoiceQuestion Question(int i) => new("c" + i.ToString("D5", System.Globalization.CultureInfo.InvariantCulture), new string('p', 700), [new string('a', 150), new string('b', 150)]);

    private static List<ChoiceQuestion> Questions(int count) => [.. Enumerable.Range(0, count).Select(Question)];

    [Theory]
    [InlineData(1_000, new int[0], 250)]
    [InlineData(40_000, new[] { 32, 32, 32, 32, 32, 32, 32, 26 }, 0)]
    [InlineData(160_000, new[] { 100, 100, 50 }, 0)]
    [InlineData(256_000, new[] { 100, 100, 50 }, 0)]
    public void Pick_packs_follow_eighty_percent_of_the_cap_and_a_hundred_questions(int cap, int[] sizes, int unfit)
    {
        var plan = LabelPacks.Pick(Questions(250), cap);

        Assert.Equal(sizes, plan.Packs.Select(p => p.Count));
        Assert.Equal(unfit, plan.Unfit.Count);
    }

    [Fact]
    public void A_pick_pack_never_holds_more_characters_than_eighty_percent_of_the_cap()
    {
        foreach (var cap in new[] { 5_000, 40_000, 160_000 })
        {
            var plan = LabelPacks.Pick(Questions(250), cap);

            Assert.All(plan.Packs, pack => Assert.True(pack.Sum(q => q.Prompt.Length + q.Options.Sum(o => o.Length)) <= (int)(cap * 0.8), "cap " + cap.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
    }

    [Fact]
    public void Packs_keep_the_order_given_so_a_rerun_builds_the_same_ones()
    {
        var questions = Questions(70);

        var first = LabelPacks.Pick(questions, 40_000);
        var again = LabelPacks.Pick(questions, 40_000);

        Assert.Equal(first.Packs.SelectMany(p => p).Select(q => q.Key), questions.Select(q => q.Key), StringComparer.Ordinal);
        Assert.Equal(first.Packs.Select(p => p[0].Key), again.Packs.Select(p => p[0].Key), StringComparer.Ordinal);
    }

    [Fact]
    public void A_question_with_no_paths_still_fits()
    {
        var bare = new ChoiceQuestion("c1", string.Empty, ["a", "b"]);

        var plan = LabelPacks.Pick([bare], 1_000);

        _ = Assert.Single(plan.Packs.Single());
    }

    [Fact]
    public void A_question_bigger_than_the_whole_budget_is_unfit_while_the_rest_are_packed()
    {
        var huge = new ChoiceQuestion("c0", new string('x', 50_000), ["a", "b"]);

        var plan = LabelPacks.Pick([huge, Question(1), Question(2)], 40_000);

        Assert.Equal(["c0"], plan.Unfit.Select(q => q.Key), StringComparer.Ordinal);
        Assert.Equal(["c00001", "c00002"], plan.Packs.Single().Select(q => q.Key), StringComparer.Ordinal);
    }

    [Fact]
    public void First_fit_counts_the_fixed_text_and_stops_at_the_item_limit()
    {
        var plan = LabelPacks.First(Enumerable.Range(0, 10), _ => 100, budgetChars: 700, maxItems: 4, fixedChars: 200);

        Assert.Equal([4, 4, 2], plan.Packs.Select(p => p.Count));   // 200 + 5 x 100 = 700 would fit five; the limit says four
        Assert.Empty(plan.Unfit);

        var tight = LabelPacks.First(Enumerable.Range(0, 10), _ => 100, budgetChars: 700, maxItems: 50, fixedChars: 200);

        Assert.Equal([5, 5], tight.Packs.Select(p => p.Count));
    }
}
