using Aw.Context.Core.Models;
using Aw.Context.Core.Surfaces;
using Aw.Context.Infrastructure.Scrubbing;
using Aw.Context.Infrastructure.Surfaces;
using Aw.Context.Tests.Infrastructure;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Surfaces;

/// <summary>The question for one group, the numbered options and the reading of the answer by position (spec 6.9 "The options", "When Jev can't decide").</summary>
[Trait("Lane", "unit")]
public sealed class SurfacePromptTests
{
    private static readonly Aw.Context.Core.Trust.IEgressScrubber _egress = new RedactionScrubberFactory(new FakeTimeProvider()).For([]).Value.Egress;

    private static ChoiceQuestion Question(SurfaceGroup group) => SurfacePrompt.Question(group, _egress);

    private static SurfaceVerdict Read(SurfaceGroup group, int? option, double confidence = 0.9)
    {
        var question = Question(group);
        return SurfacePrompt.Verdict(group, question, option is { } i ? new ChoiceAnswer(question.Key, question.Options[i], confidence) : null);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 5)]
    public void A_group_of_one_two_or_three_candidates_has_two_four_or_five_options(int candidates, int options) =>
        Assert.Equal(options, Question(SurfaceTestData.Group([.. Enumerable.Range(0, candidates).Select(i => "Name " + i.ToString(System.Globalization.CultureInfo.InvariantCulture))])).Options.Count);

    [Fact]
    public void The_options_are_numbered_separate_first_one_merge_per_candidate_and_reject_last()
    {
        var question = Question(SurfaceTestData.Group("Grant access", "Share access"));

        Assert.Equal(["1. separate", "2. merge as: Grant access", "3. merge as: Share access", "4. reject"], question.Options);
    }

    [Fact]
    public void The_question_key_is_the_groups_key()
    {
        var group = SurfaceTestData.Group("Grant access");

        Assert.Equal(group.Key, Question(group).Key);
    }

    [Fact]
    public void A_group_of_four_or_none_is_refused()
    {
        _ = Assert.Throws<ArgumentException>(() => Question(SurfaceTestData.Group("a", "b", "c", "d")));
        _ = Assert.Throws<ArgumentException>(() => Question(new SurfaceGroup([])));
    }

    [Fact]
    public void The_prompt_lists_each_candidates_node_key_kind_name_pass_evidence_and_edges_in_a_block_it_calls_data()
    {
        var prompt = Question(new SurfaceGroup([SurfaceTestData.Candidate("Grant access", "action:inclusions.grant", "src/grant.ts")])).Prompt;

        Assert.Contains("data", prompt, StringComparison.Ordinal);
        Assert.Contains("action:inclusions.grant", prompt, StringComparison.Ordinal);
        Assert.Contains("Grant access", prompt, StringComparison.Ordinal);
        Assert.Contains("actions", prompt, StringComparison.Ordinal);
        Assert.Contains("src/grant.ts", prompt, StringComparison.Ordinal);
        Assert.Contains("writes thing:account", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Evidence_and_edges_stop_at_six_and_every_line_is_flat_and_at_most_200_characters()
    {
        var evidence = Enumerable.Range(0, 9).Select(i => "src/e" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ts").ToArray();
        var edges = Enumerable.Range(0, 9).Select(i => "on surface:s" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("writes\nthing:" + new string('x', 500)).ToArray();
        var candidate = new SurfaceCandidate("action:a", "action", "A", "actions", evidence, edges);

        var prompt = Question(new SurfaceGroup([candidate])).Prompt;

        Assert.Contains("src/e5.ts", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("src/e6.ts", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("on surface:s6", prompt, StringComparison.Ordinal);
        Assert.All(prompt.Split('\n'), line => Assert.True(line.Length <= 260, line.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.DoesNotContain(new string('x', 201), prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void A_secret_in_a_name_a_path_or_an_edge_never_reaches_the_question()
    {
        var candidate = new SurfaceCandidate("action:a", "action", "Key " + Planted.Secret, "actions", [$"src/{Planted.Secret}.ts"], ["writes thing:" + Planted.Secret]);

        var question = Question(new SurfaceGroup([candidate]));

        Assert.DoesNotContain(Planted.Secret, question.Prompt + string.Concat(question.Options), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void A_confident_answer_maps_to_its_verdict_by_position(int option)
    {
        var group = SurfaceTestData.Group("Grant access", "Share access");   // options: separate, merge A, merge B, reject

        var verdict = Read(group, option);

        Assert.Equal(group.Key, verdict.GroupKey);
        Assert.Equal(option switch { 0 => SurfaceChoice.Separate, 3 => SurfaceChoice.Reject, _ => SurfaceChoice.Merge }, verdict.Choice);
        Assert.True(verdict.Decided);
        Assert.Equal(option switch { 1 => "Grant access", 2 => "Share access", _ => null }, verdict.MergedName);
    }

    [Fact]
    public void A_merge_names_the_survivor_by_index_and_by_node_key_even_when_two_candidates_share_a_name()
    {
        var group = new SurfaceGroup([SurfaceTestData.Candidate("Billing", "feature:billing"), SurfaceTestData.Candidate("Billing", "surface:billing")]);

        var verdict = Read(group, 2);

        Assert.Equal((SurfaceChoice.Merge, 1, "surface:billing", "Billing"), (verdict.Choice, verdict.Survivor, verdict.SurvivorKey, verdict.MergedName));
        Assert.Equal(group.Candidates[verdict.Survivor!.Value].NodeKey, verdict.SurvivorKey);
        Assert.Null(Read(group, 0).Survivor);
        Assert.Null(Read(group, 3).SurvivorKey);
    }

    [Fact]
    public void A_group_of_one_answers_separate_or_reject()
    {
        var group = SurfaceTestData.Group("Grant access");

        Assert.Equal(SurfaceChoice.Separate, Read(group, 0).Choice);
        Assert.Equal(SurfaceChoice.Reject, Read(group, 1).Choice);
    }

    [Theory]
    [InlineData(1, 0.49)]
    [InlineData(2, 0.49)]
    [InlineData(3, 0.49)]
    [InlineData(0, 0.49)]
    public void Confidence_under_one_half_keeps_the_group_separate_and_undecided(int option, double confidence)
    {
        var verdict = Read(SurfaceTestData.Group("Grant access", "Share access"), option, confidence);

        Assert.Equal((SurfaceChoice.Separate, false, null), (verdict.Choice, verdict.Decided, verdict.MergedName));
    }

    [Fact]
    public void A_reject_or_a_merge_at_exactly_one_half_acts()
    {
        var group = SurfaceTestData.Group("Grant access", "Share access");

        Assert.Equal(SurfaceChoice.Reject, Read(group, 3, 0.5).Choice);
        Assert.Equal(SurfaceChoice.Merge, Read(group, 1, 0.5).Choice);
    }

    [Fact]
    public void Separate_chosen_at_one_half_is_a_decision()
    {
        var verdict = Read(SurfaceTestData.Group("Grant access"), 0, 0.5);

        Assert.Equal((SurfaceChoice.Separate, true), (verdict.Choice, verdict.Decided));
    }

    [Fact]
    public void A_missing_answer_and_an_answer_with_nothing_picked_keep_the_group_separate_and_undecided()
    {
        var group = SurfaceTestData.Group("Grant access", "Share access");
        var question = Question(group);

        Assert.Equal(SurfaceVerdict.Separate(group.Key, decided: false), SurfacePrompt.Verdict(group, question, answer: null));
        Assert.Equal(SurfaceVerdict.Separate(group.Key, decided: false), SurfacePrompt.Verdict(group, question, new ChoiceAnswer(question.Key, Picked: null, 0.9)));
    }

    [Theory]
    [InlineData("separate")]                       // the words without their number are not an option
    [InlineData("2. merge as: grant access")]      // a near-match name (case) is not a merge
    [InlineData("2. merge as: Something else")]
    [InlineData("5. maybe")]
    public void An_answer_that_is_not_one_of_the_offered_options_exactly_keeps_the_group_separate_and_undecided(string picked)
    {
        var group = SurfaceTestData.Group("Grant access", "Share access");
        var question = Question(group);

        var verdict = SurfacePrompt.Verdict(group, question, new ChoiceAnswer(question.Key, picked, 0.9));

        Assert.Equal(SurfaceVerdict.Separate(group.Key, decided: false), verdict);
    }

    [Fact]
    public void A_candidate_named_separate_or_reject_is_matched_by_position_not_by_its_words()
    {
        var group = SurfaceTestData.Group("separate", "reject");
        var question = Question(group);

        Assert.Equal(["1. separate", "2. merge as: separate", "3. merge as: reject", "4. reject"], question.Options);
        Assert.Equal(SurfaceChoice.Separate, Read(group, 0).Choice);
        Assert.Equal(("separate", SurfaceChoice.Merge), (Read(group, 1).MergedName, Read(group, 1).Choice));
        Assert.Equal(("reject", SurfaceChoice.Merge), (Read(group, 2).MergedName, Read(group, 2).Choice));
        Assert.Equal(SurfaceChoice.Reject, Read(group, 3).Choice);
    }

    [Fact]
    public void Two_candidates_with_the_same_name_still_have_distinct_options()
    {
        var group = new SurfaceGroup([SurfaceTestData.Candidate("Grant access", "action:a"), SurfaceTestData.Candidate("Grant access", "action:b")]);

        var options = Question(group).Options;

        Assert.Equal(options.Count, options.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("Grant access", Read(group, 2).MergedName);
    }

    [Fact]
    public void The_merged_name_is_the_candidates_own_name_cleaned_like_a_label()
    {
        var group = new SurfaceGroup([SurfaceTestData.Candidate("  Grant\n   access\t", "action:a"), SurfaceTestData.Candidate(new string('n', 90), "action:b")]);

        Assert.Equal("Grant access", Read(group, 1).MergedName);
        Assert.Equal(new string('n', 60), Read(group, 2).MergedName);
    }
}
