using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Features.SyncSources;

namespace Aw.Context.Tests.SynthesizeConcepts;

/// <summary>The synthesis run groups the whole record store by meaning and has the model name each changed group once (#1597).</summary>
[Trait("Lane", "unit")]
public sealed class ConceptSynthesisRunTests
{
    private static CancellationToken Ct => SynthesisRig.Ct;

    private static async Task<SynthesisRig> Checkout()
    {
        var rig = new SynthesisRig();
        await rig.Add("linear", "linear:issue/ENG-1", 0);
        await rig.Add("github", "github:pr/acme/shop/2", 5);
        await rig.Add("slack", "slack:msg/C1/3", 10);
        return rig;
    }

    [Fact]
    public async Task Records_from_three_sources_about_one_thing_become_one_concept_citing_all_three()
    {
        var rig = await Checkout();

        var report = await rig.Run();

        Assert.Null(report.Code);
        var concept = Assert.Single((await rig.Concepts.All(Ct)).Value);
        Assert.Equal("checkout-retry", concept.Key);
        Assert.Equal("Checkout retry", concept.Name);
        Assert.Equal(SynthesisState.Done, concept.State);
        Assert.Equal(["github", "linear", "slack"], concept.Members.Select(m => m.Source).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal((1, 1, 1, 1, 3), (report.Groups, report.Labeled, report.Concepts, report.MultiSource, report.Embedded));
        Assert.Equal(1.0, report.Coverage);
        Assert.Equal(1, rig.Asked);
    }

    [Fact]
    public async Task An_unchanged_group_is_not_sent_to_the_model_on_the_next_run()
    {
        var rig = await Checkout();

        _ = await rig.Run();
        var second = await rig.Run();

        Assert.Equal(1, rig.Asked);
        Assert.Equal((1, 0), (second.Unchanged, second.Labeled));
    }

    [Fact]
    public async Task A_record_joining_a_group_relabels_the_same_concept_key()
    {
        var rig = await Checkout();
        _ = await rig.Run();
        var first = Assert.Single((await rig.Concepts.All(Ct)).Value);

        rig.Clock.Advance(TimeSpan.FromHours(1));
        await rig.Add("docs", "docs:doc/retry.md", 12);
        rig.Name = "Checkout retries and refunds";
        rig.Summary = "Failed payments are retried, then refunded.";
        var second = await rig.Run();

        var concept = Assert.Single((await rig.Concepts.All(Ct)).Value);
        Assert.Equal(first.Key, concept.Key);
        Assert.Equal("Checkout retries and refunds", concept.Name);
        Assert.Equal("Failed payments are retried, then refunded.", concept.Summary);
        Assert.Equal(4, concept.Members.Count);
        Assert.Equal(first.CreatedAt, concept.CreatedAt);
        Assert.Equal((1, 0), (second.Labeled, second.Unchanged));
        Assert.Equal(2, rig.Asked);
    }

    [Fact]
    public async Task The_budget_leaves_the_rest_queued_and_the_next_run_finishes_them()
    {
        var rig = await TwoTopics();
        var probe = await TwoTopics();
        _ = await probe.Run();
        var oneCall = ((probe.Model.Requests[0].Prompt.Length + 3) / 4) + ConceptSynthesisRun.AnswerTokens;

        var first = await rig.Run(maxTokens: oneCall + 10);

        Assert.Equal((1, 1, 2), (first.Labeled, first.Queued, first.Groups));
        _ = Assert.Single((await rig.Concepts.All(Ct)).Value);
        Assert.Equal(0.5, first.Coverage);

        var second = await rig.Run(maxTokens: oneCall + 10);

        Assert.Equal((1, 1, 0), (second.Unchanged, second.Labeled, second.Queued));
        Assert.Equal(["alpha-topic", "beta-topic"], (await rig.Concepts.All(Ct)).Value.Select(c => c.Key), StringComparer.Ordinal);
        Assert.Equal(2, rig.Asked);
    }

    [Fact]
    public async Task A_bad_model_answer_leaves_the_group_queued()
    {
        var rig = await Checkout();
        _ = await rig.Run();
        await rig.Add("docs", "docs:doc/retry.md", 12);
        rig.Answer = _ => "I cannot answer that";

        var bad = await rig.Run();

        var queued = Assert.Single((await rig.Concepts.All(Ct)).Value);
        Assert.Equal(SynthesisState.Queued, queued.State);
        Assert.Equal(4, queued.Members.Count);
        Assert.Equal("Checkout retry", queued.Name);
        Assert.Equal((0, 1), (bad.Labeled, bad.Queued));

        rig.Answer = null;
        rig.Name = "Checkout retries";
        var good = await rig.Run();

        var done = Assert.Single((await rig.Concepts.All(Ct)).Value);
        Assert.Equal((SynthesisState.Done, "checkout-retry", "Checkout retries"), (done.State, done.Key, done.Name));
        Assert.Equal((1, 0), (good.Labeled, good.Queued));
    }

    [Fact]
    public async Task A_bad_answer_for_a_new_group_stores_nothing_and_the_next_run_asks_again()
    {
        var rig = await Checkout();
        rig.Answer = _ => "{\"name\":\"\"}";

        var bad = await rig.Run();

        Assert.Empty((await rig.Concepts.All(Ct)).Value);
        Assert.Equal(1, bad.Queued);

        rig.Answer = null;
        var good = await rig.Run();

        _ = Assert.Single((await rig.Concepts.All(Ct)).Value);
        Assert.Equal((1, 2), (good.Labeled, rig.Asked));
    }

    [Fact]
    public async Task Records_without_vectors_count_as_pending()
    {
        var rig = await Checkout();
        await rig.AddUnembedded("docs", "docs:doc/new.md");

        var report = await rig.Run();

        Assert.Equal((1, 3), (report.Pending, report.Embedded));
        Assert.Equal(1.0, report.Coverage);
    }

    [Fact]
    public async Task A_concept_whose_records_are_all_gone_is_removed()
    {
        var rig = await Checkout();
        _ = await rig.Run();
        await rig.Remove("linear", "linear:issue/ENG-1");
        await rig.Remove("github", "github:pr/acme/shop/2");
        await rig.Remove("slack", "slack:msg/C1/3");

        var report = await rig.Run();

        Assert.Empty((await rig.Concepts.All(Ct)).Value);
        Assert.Equal(0, report.Concepts);
    }

    [Fact]
    public async Task The_run_saves_the_embedding_watermark()
    {
        var rig = await Checkout();

        _ = await rig.Run();

        Assert.Equal(3, rig.Concepts.SavedWatermark);
    }

    // Two tight pairs 90 degrees apart, every key the same length so both prompts cost the same.
    private static async Task<SynthesisRig> TwoTopics()
    {
        var rig = new SynthesisRig
        {
            Answer = request => request.Prompt.Contains("docs:doc/a1", StringComparison.Ordinal)
                ? SynthesisRig.Json("Alpha topic", "The first.")
                : SynthesisRig.Json("Beta topic", "The second."),
        };
        await rig.Add("docs", "docs:doc/a1", 0);
        await rig.Add("docs", "docs:doc/a2", 5);
        await rig.Add("docs", "docs:doc/b1", 90);
        await rig.Add("docs", "docs:doc/b2", 95);
        return rig;
    }
}
