using Aw.Context.Core;
using Aw.Context.Core.Models;
using Aw.Judge.Core.Providers;
using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>A prompt sized for the smallest harness (spec 6.2): the router tells the labeler the budget and skips a harness that would cut the prompt.</summary>
public sealed class RoutedModelPortPromptTests
{
    private static ScriptedCli Claude(params ProviderResult[] script) => new(ProviderName.ClaudeCli, script);

    private static ScriptedCli Codex(params ProviderResult[] script) => new(ProviderName.CodexCli, script);

    private static ScriptedCli Cursor(params ProviderResult[] script) => new(ProviderName.CursorCli, script);

    private static ProviderDecided Said(string text) => RouterRig.Answered(text);

    private static ModelRequest Long(int chars) => new(ModelPurpose.Labeling, new string('x', chars), 40, TimeSpan.FromSeconds(30)) { MaxPromptChars = 60_000 };

    [Fact, Trait("Lane", "unit")]
    public async Task A_harness_whose_prompt_cap_is_below_the_prompt_is_skipped_as_too_long_and_the_next_one_answers()
    {
        var cursor = Cursor(Said("never asked"));
        var claude = Claude(Said("ok"));
        var rig = new RouterRig(RouterRig.Settings("""{"labeling":{"harnesses":["cursor","claude-code"]}}"""), cursor, claude);
        using var job = rig.EnterJob();

        var answer = (await rig.Ask(Long(40_000))).Value;

        Assert.Equal("claude-code", answer.Harness);
        Assert.Empty(cursor.Asked);
        var skipped = Assert.Single(Assert.Single(rig.Calls.Calls).Tries, t => t.Harness == "cursor");
        Assert.Equal((ModelCallOutcome.Skipped, "prompt-too-long"), (skipped.Outcome, skipped.Reason));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_prompt_that_fits_every_harness_skips_none_and_a_request_without_the_cap_is_never_skipped()
    {
        var cursor = Cursor(Said("ok"));
        var rig = new RouterRig(settings: null, cursor);

        _ = await rig.Ask(Long(20_000));
        _ = await rig.Ask(new ModelRequest(ModelPurpose.Labeling, new string('x', 40_000), 40, TimeSpan.FromSeconds(30)));

        Assert.Equal(2, cursor.Asked.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task When_every_harness_is_too_small_for_the_prompt_nothing_is_asked_and_the_call_fails()
    {
        var cursor = Cursor(Said("never asked"));
        var rig = new RouterRig(settings: null, cursor);

        var answer = await rig.Ask(Long(40_000));

        Assert.Equal(ContextErrors.LabelingUnavailable.Code, answer.Error.Code);
        Assert.Empty(cursor.Asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_budget_is_the_smallest_among_the_installed_harnesses_of_the_route()
    {
        var wide = new RouterRig(settings: null, Claude(Said("x")), Codex(Said("x")));
        var mixed = new RouterRig(settings: null, Claude(Said("x")), Cursor(Said("x")));
        var narrow = new RouterRig(settings: null, Cursor(Said("x")));
        var none = new RouterRig(settings: null);
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal(PromptBudget.Wide, await wide.Port.For(ModelPurpose.Labeling, ct));
        Assert.Equal(PromptBudget.Narrow, await mixed.Port.For(ModelPurpose.Labeling, ct));
        Assert.Equal(PromptBudget.Narrow, await narrow.Port.For(ModelPurpose.Labeling, ct));
        Assert.Null(await none.Port.For(ModelPurpose.Labeling, ct));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_budget_follows_the_profiles_harness_list_not_just_what_is_installed()
    {
        var rig = new RouterRig(RouterRig.Settings("""{"labeling":{"harnesses":["claude-code"]}}"""), Claude(Said("x")), Cursor(Said("x")));

        Assert.Equal(PromptBudget.Wide, await rig.Port.For(ModelPurpose.Labeling, TestContext.Current.CancellationToken));
    }
}
