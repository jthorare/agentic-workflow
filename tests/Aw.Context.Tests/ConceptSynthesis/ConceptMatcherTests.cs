using Aw.Context.Core.ConceptSynthesis;

namespace Aw.Context.Tests.ConceptSynthesis;

/// <summary>Which stored concept a fresh group continues, the key a new concept gets and what an answer must look like.</summary>
public sealed class ConceptMatcherTests
{
    private static readonly float[] _vector = [1f];
    private static readonly DateTimeOffset _now = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);

    private static ConceptGroup Group(params string[] records) =>
        new([.. records.Select(r => new RecordVector(r, "v1", "docs", _vector))], string.Join(',', records));

    private static SynthesizedConcept Stored(string key, string hash, SynthesisState state, params string[] records) =>
        new(key, key, "s", [.. records.Select(r => new ConceptMember(r, "v1", "docs"))], hash, state, Watermark: 1, _now, _now);

    [Fact, Trait("Lane", "unit")]
    public void The_same_membership_is_the_same_concept_and_unchanged_when_it_is_labeled()
    {
        var group = Group("a", "b");

        var plan = Assert.Single(ConceptMatcher.Plan([group], [Stored("one", group.MembershipHash, SynthesisState.Done, "a", "b")]));

        Assert.True(plan.Unchanged);
        Assert.Equal("one", plan.Existing?.Key);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_membership_still_queued_is_asked_again()
    {
        var group = Group("a", "b");

        var plan = Assert.Single(ConceptMatcher.Plan([group], [Stored("one", group.MembershipHash, SynthesisState.Queued, "a", "b")]));

        Assert.False(plan.Unchanged);
        Assert.Equal("one", plan.Existing?.Key);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_stored_concept_with_the_most_shared_records_is_continued_and_the_other_group_is_new()
    {
        var plans = ConceptMatcher.Plan([Group("a", "b", "c"), Group("d", "e")], [Stored("big", "x", SynthesisState.Done, "a", "b", "d")]);

        Assert.Equal("big", plans[0].Existing?.Key);
        Assert.Null(plans[1].Existing);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_group_that_shares_nothing_is_new()
    {
        var plan = Assert.Single(ConceptMatcher.Plan([Group("a", "b")], [Stored("other", "x", SynthesisState.Done, "z", "y")]));

        Assert.Null(plan.Existing);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_key_is_a_lowercase_hyphenated_slug_made_unique_with_a_number()
    {
        Assert.Equal("checkout-retry", ConceptKeys.Slug("  Checkout Retry! ", new HashSet<string>(StringComparer.Ordinal)));
        Assert.Equal("checkout-retry-3", ConceptKeys.Slug("Checkout retry", new HashSet<string>(StringComparer.Ordinal) { "checkout-retry", "checkout-retry-2" }));
        Assert.Equal("concept", ConceptKeys.Slug("???", new HashSet<string>(StringComparer.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_answer_needs_a_name_and_a_summary_and_may_come_in_a_code_fence()
    {
        Assert.Equal(new ConceptAnswer("Checkout retry", "Orders are retried."), ConceptAnswer.Read("```json\n{\"name\":\"Checkout retry\",\"summary\":\"Orders are retried.\"}\n```"));
        Assert.Null(ConceptAnswer.Read("{\"name\":\"Checkout retry\"}"));
        Assert.Null(ConceptAnswer.Read("{\"name\":\" \",\"summary\":\"x\"}"));
        Assert.Null(ConceptAnswer.Read("Sure! It is checkout retry."));
        Assert.Null(ConceptAnswer.Read("[1]"));
    }
}
