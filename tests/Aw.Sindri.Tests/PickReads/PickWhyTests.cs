using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.PickReads;

/// <summary>The plain sentences for why a step got its model (model-pick spec §6.1's table), one test row per row of the table.</summary>
[Lane(TestLane.Unit)]
public sealed class PickWhyTests
{
    private const string Cheapest = "so Sindri picked the cheapest model allowed.";

    internal static ModelPicked Pick(string by, string? reason = null, string? model = "haiku", bool unusual = false, string? escalatedFrom = null, string? kind = null) =>
        PickRecords.Picked(A.FirstLaunchId, InstanceAddress.From("T-1/step/1"), model: model!, by: by, reason: reason, from: escalatedFrom, kind: kind, unusual: unusual);

    public static TheoryData<string, string?, string> Table() => new()
    {
        { "jev", null, "Picked by Jev: the cheapest model it expects to pass." },
        { "constraint", null, "Only one model was allowed here." },
        { "constraint", "cap", "Only one model was allowed here (your cap)." },
        { "constraint", "pin", "Only one model was allowed here (your pin)." },
        { "constraint", "floor", "Only one model was allowed here (the step's floor)." },
        { "constraint", "step", "The recipe names this step's model." },
        { "constraint", "challenger", "Only one model was allowed here (the challenger rule)." },
        { "constraint", "only-model", "Only one model was allowed here (the agent offers no other)." },
        { "rules", "no-api-key", "Jev isn't set up on this machine, " + Cheapest },
        { "rules", "timeout", "Jev didn't answer in 20 seconds, " + Cheapest },
        { "rules", "below-threshold", "Jev wasn't sure, " + Cheapest },
        { "rules", "unknown-key", "Jev wasn't sure, " + Cheapest },
        { "rules", "jev-input-rejected", "Jev couldn't take this much input, " + Cheapest },
        { "rules", "network-error", "Jev couldn't be reached, " + Cheapest },
        { "rules", "http-500", "Jev couldn't be reached, " + Cheapest },
        { "rules", "rules", "Jev wasn't asked, " + Cheapest },
        { "rules", null, "Jev wasn't asked, " + Cheapest },
        { "default", null, "This agent reports no models, so it ran on its own default." },
        { "default", "pick-off", "Model choice is off, so this ran on the agent's default." },
    };

    [Theory]
    [MemberData(nameof(Table))]
    public void Each_by_and_reason_has_its_sentence(string by, string? reason, string sentence) =>
        Assert.Equal(sentence, PickWhy.Sentence(Pick(by, reason)));

    [Theory]
    [InlineData("refused", "Retried on sonnet after haiku was refused.")]
    [InlineData("check_failed", "Retried on sonnet after haiku failed.")]
    [InlineData(null, "Retried on sonnet after haiku failed.")]
    public void A_retry_says_which_model_failed_and_how_instead_of_the_picks_own_reason(string? kind, string sentence) =>
        Assert.Equal(sentence, PickWhy.Sentence(Pick("jev", model: "sonnet", escalatedFrom: "haiku", kind: kind)));

    [Fact]
    public void An_unusual_pick_adds_the_note_for_the_model_that_failed_most_of_its_recent_runs()
    {
        var sentence = PickWhy.Sentence(Pick("jev", model: "haiku", unusual: true));

        Assert.Equal("Picked by Jev: the cheapest model it expects to pass. Unusual: haiku failed most of its recent runs on this step.", sentence);
    }

    [Fact]
    public void An_unusual_retry_keeps_both_sentences() =>
        Assert.Equal(
            "Retried on opus after sonnet was refused. Unusual: opus failed most of its recent runs on this step.",
            PickWhy.Sentence(Pick("jev", model: "opus", unusual: true, escalatedFrom: "sonnet", kind: "refused")));

    [Fact]
    public void While_only_model_picking_exists_the_sentence_is_asking_jev() => Assert.Equal("Asking Jev.", PickWhy.Asking);
}
