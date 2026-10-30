using Aw.Context.Core;
using Aw.Context.Core.Models;
using Aw.Context.Core.Surfaces;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Scrubbing;
using Aw.Context.Infrastructure.Surfaces;
using Aw.Context.Testing.Engine;
using Aw.Context.Tests.Infrastructure;
using Aw.Kernel;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Surfaces;

/// <summary>Jev decides candidate groups (spec 6.9): one question per group, packed and split as the label pick is, and nothing asked when the pick is off.</summary>
[Trait("Lane", "unit")]
public sealed class SurfacePickerTests
{
    private static readonly SurfacePickOptions _on = new(Pick: true, [], SpentTokens: 0, MaxTokens: 1_000_000);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SurfacePicker Picker(IChoicePort? port) => new(port, new RedactionScrubberFactory(new FakeTimeProvider()));

    // Jev answers each question with the option at the given position (counted from the front; negative from the back).
    private static FakeChoicePort Answering(int option, double confidence = 0.9) => new()
    {
        Script = qs => Results.Success<IReadOnlyList<ChoiceAnswer>>([.. qs.Select(q => new ChoiceAnswer(q.Key, q.Options[option < 0 ? q.Options.Count + option : option], confidence))]),
    };

    [Fact]
    public async Task Each_group_is_one_question_asked_as_the_product_surface_kind_and_each_gets_its_verdict_in_input_order()
    {
        var port = new FakeChoicePort
        {
            Script = qs => Results.Success<IReadOnlyList<ChoiceAnswer>>([
                new ChoiceAnswer(qs[0].Key, qs[0].Options[1], 0.9),   // group 1: merge as the first name
                new ChoiceAnswer(qs[1].Key, qs[1].Options[^1], 0.9),  // group 2: reject
                new ChoiceAnswer(qs[2].Key, qs[2].Options[0], 0.9),   // group 3: separate
            ]),
        };
        var groups = new[] { SurfaceTestData.Group("Grant access", "Share access"), SurfaceTestData.Group("Export"), SurfaceTestData.Group("Invite", "Add") };

        var result = await Picker(port).Decide(groups, _on, Ct);

        var (kind, asked, _) = Assert.Single(port.Asked);
        Assert.Equal(ChoiceKind.ProductSurfaces, kind);
        Assert.Equal(groups.Select(g => g.Key), asked.Select(q => q.Key), StringComparer.Ordinal);
        Assert.Equal(groups.Select(g => g.Key), result.Verdicts.Select(v => v.GroupKey), StringComparer.Ordinal);
        Assert.Equal([SurfaceChoice.Merge, SurfaceChoice.Reject, SurfaceChoice.Separate], result.Verdicts.Select(v => v.Choice));
        Assert.Equal("Grant access", result.Verdicts[0].MergedName);
        Assert.Null(result.NotDecided);
    }

    [Fact]
    public async Task The_counts_add_up_to_the_group_count_and_undecided_is_part_of_kept()
    {
        var port = new FakeChoicePort
        {
            Script = qs => Results.Success<IReadOnlyList<ChoiceAnswer>>([
                new ChoiceAnswer(qs[0].Key, qs[0].Options[1], 0.9),    // merged
                new ChoiceAnswer(qs[1].Key, qs[1].Options[0], 0.9),    // kept, chosen
                new ChoiceAnswer(qs[2].Key, qs[2].Options[^1], 0.9),   // rejected
                new ChoiceAnswer(qs[3].Key, qs[3].Options[^1], 0.49),  // kept, undecided
                // the fifth group gets no answer: kept, undecided
            ]),
        };
        var groups = new[] { SurfaceTestData.Group("A", "B"), SurfaceTestData.Group("C"), SurfaceTestData.Group("D"), SurfaceTestData.Group("E"), SurfaceTestData.Group("F") };

        var result = await Picker(port).Decide(groups, _on, Ct);

        Assert.Equal(new SurfaceCounts(Merged: 1, Kept: 3, Rejected: 1, Undecided: 2), result.Counts);
        Assert.Equal(groups.Length, result.Counts.Merged + result.Counts.Kept + result.Counts.Rejected);
    }

    [Fact]
    public async Task With_the_pick_off_nothing_is_asked_every_group_stays_separate_and_the_status_line_says_off()
    {
        var port = Answering(-1);
        var groups = SurfaceTestData.Groups(3);

        var result = await Picker(port).Decide(groups, _on with { Pick = false }, Ct);

        Assert.Empty(port.Asked);
        Assert.All(result.Verdicts, v => Assert.Equal((SurfaceChoice.Separate, false), (v.Choice, v.Decided)));
        Assert.Equal(new SurfaceCounts(0, 3, 0, 3), result.Counts);
        Assert.Equal("off", result.NotDecided);
        Assert.Equal("product surfaces: not decided (off)", result.StatusLine);
    }

    [Fact]
    public async Task With_no_choice_port_the_result_is_the_same_as_off()
    {
        var result = await Picker(port: null).Decide(SurfaceTestData.Groups(2), _on, Ct);

        Assert.Equal(("off", 2), (result.NotDecided, result.Verdicts.Count));
        Assert.All(result.Verdicts, v => Assert.False(v.Decided));
    }

    [Fact]
    public async Task No_groups_ask_nothing_and_decide_nothing()
    {
        var port = Answering(0);

        var result = await Picker(port).Decide([], _on, Ct);

        Assert.Empty(port.Asked);
        Assert.Empty(result.Verdicts);
        Assert.Equal(new SurfaceCounts(0, 0, 0, 0), result.Counts);
    }

    [Fact]
    public async Task No_key_stops_the_pass_after_one_call_and_the_rest_stay_separate_with_the_reason()
    {
        var port = new FakeChoicePort { Script = FakeChoicePort.Failing(rejected: false, ChoiceReasons.NoKey) };
        var groups = SurfaceTestData.Groups(150);   // two packs at the 100-question limit

        var result = await Picker(port).Decide(groups, _on, Ct);

        _ = Assert.Single(port.Asked);
        Assert.Equal(150, result.Verdicts.Count);
        Assert.All(result.Verdicts, v => Assert.Equal((SurfaceChoice.Separate, false), (v.Choice, v.Decided)));
        Assert.Equal("no-key", result.NotDecided);
        Assert.Equal("product surfaces: not decided (no-key)", result.StatusLine);
    }

    [Fact]
    public async Task A_pack_is_at_most_a_hundred_groups_in_key_order()
    {
        var port = Answering(0);

        _ = await Picker(port).Decide(SurfaceTestData.Groups(101), _on, Ct);

        Assert.Equal([100, 1], port.Asked.Select(a => a.Questions.Count));
    }

    [Fact]
    public async Task A_pack_Jev_refuses_is_split_in_halves_and_each_half_is_asked()
    {
        var port = new FakeChoicePort { Script = qs => qs.Count > 2 ? ContextErrors.ChoiceRejected.With("http-413") : Results.Success<IReadOnlyList<ChoiceAnswer>>([.. qs.Select(q => new ChoiceAnswer(q.Key, q.Options[^1], 0.9))]) };

        var result = await Picker(port).Decide(SurfaceTestData.Groups(4), _on, Ct);

        Assert.Equal([4, 2, 2], port.Asked.Select(a => a.Questions.Count));
        Assert.All(result.Verdicts, v => Assert.Equal(SurfaceChoice.Reject, v.Choice));
        Assert.Null(result.NotDecided);
    }

    [Fact]
    public async Task A_refused_pack_splits_no_deeper_than_two_levels_and_the_unanswered_groups_stay_separate()
    {
        var port = new FakeChoicePort { Script = FakeChoicePort.Failing(rejected: true, "http-413") };

        var result = await Picker(port).Decide(SurfaceTestData.Groups(8), _on, Ct);

        Assert.Equal([8, 4, 2, 2, 4, 2, 2], port.Asked.Select(a => a.Questions.Count));
        Assert.All(result.Verdicts, v => Assert.False(v.Decided));
    }

    [Fact]
    public async Task Jev_tokens_count_toward_the_one_budget_and_a_pack_over_it_is_not_asked()
    {
        var port = Answering(0);

        var result = await Picker(port).Decide(SurfaceTestData.Groups(3), _on with { SpentTokens = 999_990, MaxTokens = 1_000_000 }, Ct);

        Assert.Empty(port.Asked);
        Assert.Equal("token-cap", result.NotDecided);
        Assert.All(result.Verdicts, v => Assert.False(v.Decided));
    }

    [Fact]
    public async Task A_secret_and_an_identifier_the_owner_scrubs_never_reach_the_port()
    {
        var port = Answering(0);
        var candidate = new SurfaceCandidate("action:a", "action", "Key " + Planted.Secret, "actions", [$"src/{Planted.Secret}.ts", "clients/ACME-123456/billing.ts"], ["writes thing:ACME-123456"]);

        _ = await Picker(port).Decide([new SurfaceGroup([candidate])], _on with { ExtraPatterns = [new ScrubPattern("acme-customer-id", "(?i)ACME[- ]?[0-9]{6}")] }, Ct);

        var question = Assert.Single(Assert.Single(port.Asked).Questions);
        var sent = question.Prompt + string.Concat(question.Options);
        Assert.DoesNotContain(Planted.Secret, sent, StringComparison.Ordinal);
        Assert.DoesNotContain("123456", sent, StringComparison.Ordinal);
    }
}
