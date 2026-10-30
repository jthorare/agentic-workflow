using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Models;
using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Providers;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>Jev behind the context engine's choice port (spec 6.6, 6.7, 7.1): what it sends, when it sends nothing, how its failures map to the labeler's two kinds, and the event it writes.</summary>
public sealed class JevChoicePortTests
{
    private static readonly JsonSerializerOptions _json = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // --- identity and the cap

    [Fact, Trait("Lane", "unit")]
    public void The_identity_names_the_jev_model_and_version_and_is_the_same_string_on_every_read()
    {
        var port = new ChoiceRig(ScriptedBatches.Picking()).Port;

        Assert.Equal(JevModel.Id, port.Identity.Value);
        Assert.Contains("jev-", port.Identity.Value, StringComparison.Ordinal);
        Assert.Same(port.Identity, port.Identity);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, 160_000)]
    [InlineData(40_000, 40_000)]
    [InlineData(1_000, 1_000)]
    public async Task The_cap_is_the_judges_input_cap_chars_or_its_default(int? configured, int expected)
    {
        var rig = new ChoiceRig(ScriptedBatches.Picking(), providers: configured is { } cap ? new ProvidersConfig(InputCapChars: cap) : null);

        Assert.Equal(expected, await rig.Port.InputCapChars(Token));
    }

    // --- what is sent

    [Fact, Trait("Lane", "unit")]
    public async Task A_pack_goes_as_one_batch_with_the_options_as_the_criteria_and_the_judges_providers_config()
    {
        var providers = new ProvidersConfig(InputCapChars: 90_000);
        var rig = new ChoiceRig(ScriptedBatches.Picking(), providers: providers);

        var answers = (await rig.Pick(ChoiceRig.Question("h1", "Tabs", "Tab groups"), ChoiceRig.Question("h2", "Panes", "Layout", "Split"))).Value;

        var batch = Assert.Single(rig.Batches.Asked);
        Assert.Equal(["h1", "h2"], batch.Questions.Select(q => q.Key), StringComparer.Ordinal);
        Assert.Equal(["Panes", "Layout", "Split"], Assert.IsType<ChoiceBatchQuestion>(batch.Questions[1]).Criteria.Keys, StringComparer.Ordinal);
        Assert.Null(batch.State);
        Assert.Equal(LabelPickLimits.Budget, batch.Budget);
        Assert.Same(providers, Assert.Single(rig.Batches.Configs));
        Assert.Equal([("h1", "Tab groups", 0.9), ("h2", "Split", 0.9)], answers.Select(a => (a.Key, a.Picked, a.Confidence)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Only_the_given_strings_are_in_the_request_body()
    {
        var rig = new ChoiceRig(ScriptedBatches.Picking());
        using var job = rig.EnterJob();

        _ = await rig.Pick(ChoiceRig.Question("h1", "Tabs", "Tab groups"));

        // everything the batch carries, as text: the state, and each question's key, text and criteria (the base type's JSON would leave the criteria out)
        var batch = Assert.Single(rig.Batches.Asked);
        var body = JsonSerializer.Serialize(batch.State, _json) + string.Concat(batch.Questions.Select(q => q.Key + q.Prompt + (q is ChoiceBatchQuestion c ? string.Concat(c.Criteria.Select(p => p.Key + p.Value)) : string.Empty)));
        Assert.DoesNotContain("BuildContextMap", body, StringComparison.Ordinal);
        Assert.DoesNotContain("JOB-", body, StringComparison.Ordinal);
        Assert.Contains("Tab groups", body, StringComparison.Ordinal);
        Assert.Contains("src/h1/a.ts", body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pack_over_eighty_percent_of_the_cap_is_refused_before_anything_is_sent()
    {
        var rig = new ChoiceRig(ScriptedBatches.Picking());
        var big = new ChoiceQuestion("h1", new string('x', 1_000), ["a", "b"]);

        var reply = await rig.Port.Pick(ChoiceKind.Labeling, [big], capChars: 1_000, Token);

        Assert.Equal(ContextErrors.ChoiceRejected.Code, reply.Error.Code);
        Assert.Equal(ChoiceReasons.OverCap, reply.Error.Message);
        Assert.Empty(rig.Batches.Asked);
    }

    // --- the approved profile decides, at every call

    [Fact, Trait("Lane", "unit")]
    public async Task A_pick_that_is_off_in_the_approved_profile_sends_nothing_and_says_not_asked()
    {
        var rig = new ChoiceRig(ScriptedBatches.Picking(), LabelPick.Off);
        using var job = rig.EnterJob();

        var reply = await rig.Pick(ChoiceRig.Question("h1"));

        Assert.Equal((ContextErrors.ChoiceUnavailable.Code, ChoiceReasons.Off), (reply.Error.Code, reply.Error.Message));
        Assert.Empty(rig.Batches.Asked);
        var attempt = Assert.Single(Assert.Single(rig.Calls.Calls).Tries);
        Assert.Equal(("jev", ModelCallOutcome.NotAsked, "off"), (attempt.Harness, attempt.Outcome, attempt.Reason));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_setting_is_read_from_the_approved_profile_at_every_call_so_an_edit_nobody_approved_makes_no_call()
    {
        var rig = new ChoiceRig(ScriptedBatches.Picking());

        Assert.True((await rig.Pick(ChoiceRig.Question("h1"))).IsSuccess);
        rig.Models.Pick = LabelPick.Off;   // what the approved profile says now; the live file is never read here
        Assert.True((await rig.Pick(ChoiceRig.Question("h2"))).IsError);

        _ = Assert.Single(rig.Batches.Asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_product_surface_pick_is_off_when_its_own_setting_is_off_even_if_the_label_pick_is_on()
    {
        var rig = new ChoiceRig(ScriptedBatches.Picking(), LabelPick.Jev, surfacePick: LabelPick.Off);

        var reply = await rig.PickSurfaces(ChoiceRig.Question("g1"));

        Assert.Equal(ChoiceReasons.Off, reply.Error.Message);
        Assert.Empty(rig.Batches.Asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_product_surface_pick_asks_the_classifier_when_the_approved_profile_says_jev_and_the_label_pick_is_off()
    {
        var rig = new ChoiceRig(ScriptedBatches.Picking(), LabelPick.Off, surfacePick: LabelPick.Jev);

        var answers = (await rig.PickSurfaces(ChoiceRig.Question("g1", "1. separate", "2. reject"))).Value;

        _ = Assert.Single(rig.Batches.Asked);
        Assert.Equal(("g1", "2. reject"), (answers[0].Key, answers[0].Picked));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_product_surface_setting_is_read_from_the_approved_profile_at_every_call_so_an_unapproved_edit_asks_nothing()
    {
        var rig = new ChoiceRig(ScriptedBatches.Picking(), surfacePick: LabelPick.Jev);

        Assert.True((await rig.PickSurfaces(ChoiceRig.Question("g1"))).IsSuccess);
        rig.Models.SurfacePick = LabelPick.Off;
        Assert.True((await rig.PickSurfaces(ChoiceRig.Question("g2"))).IsError);

        _ = Assert.Single(rig.Batches.Asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_product_surface_call_writes_the_purpose_the_kind_and_the_four_counts_by_option_position()
    {
        var batches = new ScriptedBatches(request => new BatchDecided(new Dictionary<string, BatchAnswer>(StringComparer.Ordinal)
        {
            ["g1"] = new ChoiceBatchAnswer("2. merge as: A", 0.9, new Dictionary<string, double>(StringComparer.Ordinal)),    // merged
            ["g2"] = new ChoiceBatchAnswer("1. separate", 0.9, new Dictionary<string, double>(StringComparer.Ordinal)),       // kept, chosen
            ["g3"] = new ChoiceBatchAnswer("3. reject", 0.9, new Dictionary<string, double>(StringComparer.Ordinal)),         // rejected
            ["g4"] = new ChoiceBatchAnswer("3. reject", 0.49, new Dictionary<string, double>(StringComparer.Ordinal)),        // kept, undecided
        }, new BatchUsage(900, 30)));
        var rig = new ChoiceRig(batches, surfacePick: LabelPick.Jev);
        using var job = rig.EnterJob();

        _ = await rig.PickSurfaces(
            ChoiceRig.Question("g1", "1. separate", "2. merge as: A", "3. reject"),
            ChoiceRig.Question("g2", "1. separate", "2. reject"),
            ChoiceRig.Question("g3", "1. separate", "2. merge as: A", "3. reject"),
            ChoiceRig.Question("g4", "1. separate", "2. merge as: A", "3. reject"),
            ChoiceRig.Question("g5", "1. separate", "2. reject"));   // left out of the answer: kept, undecided

        var called = Assert.Single(rig.Calls.Calls);
        Assert.Equal(("product-surfaces", "productSurfaces", 5), (called.Purpose, called.Kind, called.Items));
        Assert.Equal(new ModelCallHow(Merged: 1, Kept: 3, Rejected: 1, Undecided: 2), called.How);
        Assert.Null(called.How!.Jev);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_product_surface_pick_that_is_off_is_a_not_asked_try_with_its_reason()
    {
        var rig = new ChoiceRig(ScriptedBatches.Picking());
        using var job = rig.EnterJob();

        _ = await rig.PickSurfaces(ChoiceRig.Question("g1"));

        var called = Assert.Single(rig.Calls.Calls);
        var attempt = Assert.Single(called.Tries);
        Assert.Equal(("product-surfaces", "jev", ModelCallOutcome.NotAsked, "off"), (called.Purpose, attempt.Harness, attempt.Outcome, attempt.Reason));
    }

    // --- what comes back

    [Fact, Trait("Lane", "unit")]
    public async Task An_answer_that_is_not_an_offered_option_has_no_pick_and_a_question_left_out_has_no_answer()
    {
        var batches = new ScriptedBatches(request => new BatchDecided(new Dictionary<string, BatchAnswer>(StringComparer.Ordinal)
        {
            ["h1"] = new ChoiceBatchAnswer("Not offered", 0.9, new Dictionary<string, double>(StringComparer.Ordinal)),
        }));
        var rig = new ChoiceRig(batches);

        var answers = (await rig.Pick(ChoiceRig.Question("h1"), ChoiceRig.Question("h2"))).Value;

        var only = Assert.Single(answers);
        Assert.Equal("h1", only.Key);
        Assert.Null(only.Picked);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("no-api-key", false, "no-key")]
    [InlineData("network-error", false, "network-error")]
    [InlineData("excluded", false, "excluded")]
    [InlineData("timeout", true, "timeout")]
    public async Task A_provider_that_cant_answer_maps_to_rejected_or_unavailable(string reason, bool rejected, string message)
    {
        var rig = new ChoiceRig(new ScriptedBatches(_ => new ProviderUnavailable(reason)));

        var reply = await rig.Pick(ChoiceRig.Question("h1"));

        Assert.Equal((rejected ? ContextErrors.ChoiceRejected.Code : ContextErrors.ChoiceUnavailable.Code, message), (reply.Error.Code, reply.Error.Message));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("http-413", true)]
    [InlineData("http-400", true)]
    [InlineData("http-422", true)]
    [InlineData("unparseable-answer", true)]
    [InlineData("http-401", false)]
    [InlineData("http-403", false)]
    [InlineData("http-429", false)]
    [InlineData("http-500", false)]
    [InlineData("http-503", false)]
    [InlineData("provider-threw", false)]
    public async Task An_errored_provider_maps_to_rejected_when_the_input_was_refused_and_to_unavailable_otherwise(string reason, bool rejected)
    {
        var rig = new ChoiceRig(new ScriptedBatches(_ => new ProviderErrored(reason)));

        var reply = await rig.Pick(ChoiceRig.Question("h1"));

        Assert.Equal((rejected ? ContextErrors.ChoiceRejected.Code : ContextErrors.ChoiceUnavailable.Code, reason), (reply.Error.Code, reply.Error.Message));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_key_is_also_told_as_not_asked_with_the_no_key_reason()
    {
        var rig = new ChoiceRig(new ScriptedBatches(_ => new ProviderUnavailable("no-api-key")));
        using var job = rig.EnterJob();

        _ = await rig.Pick(ChoiceRig.Question("h1"));

        var attempt = Assert.Single(Assert.Single(rig.Calls.Calls).Tries);
        Assert.Equal((ModelCallOutcome.NotAsked, "no-key"), (attempt.Outcome, attempt.Reason));
    }

    // --- the event

    [Fact, Trait("Lane", "unit")]
    public async Task A_pick_call_writes_one_event_with_the_counts_and_no_text()
    {
        var batches = new ScriptedBatches(request => new BatchDecided(new Dictionary<string, BatchAnswer>(StringComparer.Ordinal)
        {
            ["h1"] = new ChoiceBatchAnswer("Alpha", 0.9, new Dictionary<string, double>(StringComparer.Ordinal)),
            ["h2"] = new ChoiceBatchAnswer("Beta", 0.49, new Dictionary<string, double>(StringComparer.Ordinal)),
        }, new BatchUsage(1200, 40)));
        var rig = new ChoiceRig(batches);
        using var job = rig.EnterJob();

        _ = await rig.Pick(ChoiceRig.Question("h1"), ChoiceRig.Question("h2"), ChoiceRig.Question("h3"));

        var called = Assert.Single(rig.Calls.Calls);
        Assert.Equal(("labeling", "labeling", 3, 1200, 40, ModelTokenBasis.Reported), (called.Purpose, called.Kind, called.Items, called.InputTokens, called.OutputTokens, called.TokenBasis));
        Assert.Equal(new ModelCallHow(Jev: 1, FirstCandidate: 2), called.How);
        Assert.Equal(("jev", ModelCallOutcome.Answered), (called.Tries[0].Harness, called.Tries[0].Outcome));
        Assert.Equal(new ModelCallAnswerer("jev", JevModel.Id), called.AnsweredBy);
        _ = Assert.NotNull(called.Job);
        var written = AwJson.Serialize(called, SliceHarness.Json);
        Assert.DoesNotContain("src/h1", System.Text.Encoding.UTF8.GetString(written), StringComparison.Ordinal);
        Assert.DoesNotContain("Alpha", System.Text.Encoding.UTF8.GetString(written), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_reported_usage_the_tokens_are_characters_over_four_and_say_estimated()
    {
        var rig = new ChoiceRig(ScriptedBatches.Picking());
        using var job = rig.EnterJob();

        _ = await rig.Pick(ChoiceRig.Question("h1"));

        var called = Assert.Single(rig.Calls.Calls);
        Assert.Equal(ModelTokenBasis.Estimated, called.TokenBasis);
        Assert.True(called.InputTokens > 0);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_call_writes_its_event_with_a_code_and_a_call_outside_a_job_that_answered_writes_none()
    {
        var failing = new ChoiceRig(new ScriptedBatches(_ => new ProviderErrored("http-429")));
        var outside = new ChoiceRig(ScriptedBatches.Picking());

        _ = await failing.Pick(ChoiceRig.Question("h1"));
        _ = await outside.Pick(ChoiceRig.Question("h1"));

        var attempt = Assert.Single(Assert.Single(failing.Calls.Calls).Tries);
        Assert.Equal((ModelCallOutcome.Failed, "http-429"), (attempt.Outcome, attempt.Reason));
        Assert.Empty(outside.Calls.Calls);
    }
}
