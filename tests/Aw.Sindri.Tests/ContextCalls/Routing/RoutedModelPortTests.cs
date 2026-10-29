using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Host.Models;
using Aw.Context.Core.Models;
using Aw.Judge.Core.Providers;
using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>The router (model routing spec §4): the way it asks the owner's agent CLIs, which it asks and for which model, what it remembers of a limit, and the event it writes.</summary>
public sealed class RoutedModelPortTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ScriptedCli Claude(params ProviderResult[] script) => new(ProviderName.ClaudeCli, script);

    private static ScriptedCli Codex(params ProviderResult[] script) => new(ProviderName.CodexCli, script);

    private static ProviderDecided Said(string text, ProviderUsage? usage = null) => RouterRig.Answered(text, usage);

    private static ProviderUnavailable Down() => new(ProviderUnavailable.Timeout);

    // --- the judge catalog's way of asking, kept from the port this one replaced

    [Fact, Trait("Lane", "unit")]
    public async Task The_first_agent_cli_that_answers_gives_the_text_and_says_which_harness_answered()
    {
        var claude = Claude(Said("""[{"id":"c1","label":"Tabs"}]"""));
        var codex = Codex(Said("never asked"));
        var rig = new RouterRig(settings: null, claude, codex);

        var answer = (await rig.Ask(RouterRig.Request())).Value;

        Assert.Equal("""[{"id":"c1","label":"Tabs"}]""", answer.Text);
        Assert.Equal("claude-code", answer.Harness);
        Assert.True(answer.InputTokens > 0);
        Assert.True(answer.OutputTokens > 0);
        Assert.Equal(TokenBasis.Estimated, answer.TokenBasis);
        _ = Assert.Single(claude.Asked);
        Assert.Empty(codex.Asked);
        Assert.Contains("c1: src/a.ts, src/b.ts", claude.Asked[0].Prompt, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_remote_jev_api_is_never_allowed_because_the_prompt_names_files_of_the_owners_repo()
    {
        var rig = new RouterRig(settings: null, Claude(Said("x")));

        _ = await rig.Ask(RouterRig.Request());

        Assert.DoesNotContain(ProviderName.Jev, rig.Catalog.Allowed!);
        Assert.Contains(ProviderName.ClaudeCli, rig.Catalog.Allowed!);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_decision_without_text_is_not_an_answer()
    {
        var rig = new RouterRig(settings: null, Claude(new ProviderDecided("answered", 0.9, "ok")));

        var answer = await rig.Ask(RouterRig.Request());

        Assert.Equal(ContextErrors.LabelingUnavailable.Code, answer.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_question_asks_for_the_minimal_environment()
    {
        var claude = Claude(Said("ok"));
        var rig = new RouterRig(settings: null, claude);

        _ = await rig.Ask(RouterRig.Request());

        Assert.True(Assert.Single(claude.Asked).MinimalEnvironment);
    }

    // --- which model each harness runs

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_with_nothing_in_it_asks_each_harness_for_its_built_in_cheap_name()
    {
        var claude = Claude(Down());
        var codex = Codex(Said("ok"));
        var rig = new RouterRig(settings: null, claude, codex);

        var answer = (await rig.Ask(RouterRig.Request())).Value;

        Assert.Equal("haiku", Assert.Single(claude.Asked).Model);
        Assert.Equal("gpt-6-luna", Assert.Single(codex.Asked).Model);
        Assert.Equal("gpt-6-luna", answer.Model);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_profile_orders_the_harnesses_and_names_the_model_of_a_kind()
    {
        var claude = Claude(Said("never asked"));
        var codex = Codex(Said("ok"));
        var settings = RouterRig.Settings("""{"labeling":{"harnesses":["codex","claude-code"],"models":{"codex":"gpt-x"}}}""");
        var rig = new RouterRig(settings, claude, codex);
        using var job = rig.EnterJob();   // a call outside a job that answered writes no event

        var answer = (await rig.Ask(RouterRig.Request())).Value;

        Assert.Equal("codex", answer.Harness);
        Assert.Equal("gpt-x", Assert.Single(codex.Asked).Model);
        Assert.Empty(claude.Asked);
        Assert.Equal("kind.models", Assert.Single(rig.Calls.Calls).Tries[0].Source);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_request_that_names_models_wins_over_the_profile()
    {
        var claude = Claude(Down());
        var codex = Codex(Said("ok"));
        var settings = RouterRig.Settings("""{"labeling":{"models":{"claude-code":"opus"}}}""");
        var rig = new RouterRig(settings, claude, codex);
        var models = new Dictionary<string, string>(StringComparer.Ordinal) { ["claude"] = "m-claude" };

        var answer = (await rig.Ask(RouterRig.Request() with { Models = models, Model = "m-any" })).Value;

        Assert.Equal("m-claude", Assert.Single(claude.Asked).Model);
        Assert.Equal("m-any", Assert.Single(codex.Asked).Model);
        Assert.Equal("m-any", answer.Model);
        Assert.All(Assert.Single(rig.Calls.Calls).Tries, t => Assert.Equal("request", t.Source));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--dangerously-skip-permissions")]
    [InlineData("two words")]
    [InlineData("")]
    public async Task A_model_name_in_the_request_that_fails_the_name_check_reaches_no_cli(string name)
    {
        var claude = Claude(Said("never asked"));
        var rig = new RouterRig(settings: null, claude);

        var answer = await rig.Ask(RouterRig.Request() with { Model = name });

        Assert.True(answer.IsError);
        Assert.Empty(claude.Asked);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(ModelPurpose.Concepts)]
    [InlineData(ModelPurpose.ConceptScan)]
    public async Task The_concept_names_and_the_concept_scan_both_use_the_conceptNaming_entry(ModelPurpose purpose)
    {
        var claude = Claude(Said("ok"));
        var rig = new RouterRig(RouterRig.Settings("""{"conceptNaming":{"models":{"claude-code":"sonnet-x"}}}"""), claude);
        using var job = rig.EnterJob();   // a call outside a job that answered writes no event

        _ = await rig.Ask(RouterRig.Request(purpose));

        Assert.Equal("sonnet-x", Assert.Single(claude.Asked).Model);
        Assert.Equal("conceptNaming", Assert.Single(rig.Calls.Calls).Kind);
    }

    // --- per-job limit memory (spec 5.2)

    [Fact, Trait("Lane", "unit")]
    public async Task A_harness_that_hit_a_limit_is_skipped_for_the_rest_of_the_job()
    {
        var claude = Claude(RouterRig.Limit());
        var codex = Codex(Said("one"), Said("two"));
        var rig = new RouterRig(settings: null, claude, codex);
        using var job = rig.EnterJob();

        _ = await rig.Ask(RouterRig.Request());
        _ = await rig.Ask(RouterRig.Request());

        _ = Assert.Single(claude.Asked);
        Assert.Equal(2, codex.Asked.Count);
        var second = rig.Calls.Calls[1].Tries;
        Assert.Equal(("claude-code", ModelCallOutcome.Skipped, "limited"), (second[0].Harness, second[0].Outcome, second[0].Reason));
        Assert.Equal(ModelCallOutcome.Answered, second[1].Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_harness_limited_in_one_job_is_tried_again_at_the_start_of_another()
    {
        var claude = Claude(RouterRig.Limit(), Said("back"));
        var codex = Codex(Said("fallback"));
        var rig = new RouterRig(settings: null, claude, codex);
        using (rig.EnterJob("a"))
        {
            _ = await rig.Ask(RouterRig.Request());
        }

        using var other = rig.EnterJob("b");
        var answer = (await rig.Ask(RouterRig.Request())).Value;

        Assert.Equal(2, claude.Asked.Count);
        Assert.Equal("claude-code", answer.Harness);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_call_outside_a_job_keeps_no_memory_of_a_limit()
    {
        var claude = Claude(RouterRig.Limit());
        var codex = Codex(Said("one"));
        var rig = new RouterRig(settings: null, claude, codex);

        _ = await rig.Ask(RouterRig.Request());
        _ = await rig.Ask(RouterRig.Request());

        Assert.Equal(2, claude.Asked.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_entry_of_a_job_that_ended_is_dropped_and_the_others_stay()
    {
        var limits = new JobModelLimits();
        var ended = new JobContext(Aw.Sindri.Core.Ids.JobId.From("JOB-" + new string('c', 32)), Aw.Sindri.Core.Ids.JobKind.From("BuildContextMap"), new LineProgress());
        var running = new JobContext(Aw.Sindri.Core.Ids.JobId.From("JOB-" + new string('d', 32)), Aw.Sindri.Core.Ids.JobKind.From("BuildContextMap"), new LineProgress());
        limits.Remember(ended, ModelHarness.ClaudeCode);
        limits.Remember(running, ModelHarness.ClaudeCode);

        var endedEntry = limits.EntryFor(ended);   // read before pruning: asking again afterwards would make a fresh one

        limits.Prune(entry => entry == endedEntry);

        Assert.Equal(1, limits.Count);
        Assert.True(limits.IsLimited(running, ModelHarness.ClaudeCode));
        Assert.False(limits.IsLimited(ended, ModelHarness.ClaudeCode));
    }

    // --- the event (spec 7.1)

    [Fact, Trait("Lane", "unit")]
    public async Task One_event_lists_every_try_with_a_code_and_never_text()
    {
        var claude = Claude(RouterRig.Limit());
        var codex = Codex(Said("answer text that must not be recorded", new ProviderUsage(120, 30) { Model = "gpt-6-luna-2" }));
        var rig = new RouterRig(settings: null, claude, codex);
        using var job = rig.EnterJob();

        var answer = (await rig.Ask(RouterRig.Request())).Value;

        var called = Assert.Single(rig.Calls.Calls);
        Assert.Equal(("labeling", "labeling", 120, 30, ModelTokenBasis.Reported), (called.Purpose, called.Kind, called.InputTokens, called.OutputTokens, called.TokenBasis));
        Assert.Equal(JobId(), called.Job?.Value);
        Assert.Equal(("claude-code", "haiku", ModelCallOutcome.Limit, "limit", "builtin"), (called.Tries[0].Harness, called.Tries[0].Model, called.Tries[0].Outcome, called.Tries[0].Reason, called.Tries[0].Source));
        Assert.Equal(("codex", ModelCallOutcome.Answered), (called.Tries[1].Harness, called.Tries[1].Outcome));
        Assert.Null(called.Tries[1].Reason);
        Assert.Equal(("codex", "gpt-6-luna-2"), (called.AnsweredBy!.Harness, called.AnsweredBy.Model));
        Assert.Equal((120, 30, TokenBasis.Reported, "gpt-6-luna-2"), (answer.InputTokens, answer.OutputTokens, answer.TokenBasis, answer.Model));
        Assert.DoesNotContain("must not be recorded", called.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_purpose_in_the_event_is_the_name_the_profile_docs_use()
    {
        var rig = new RouterRig(settings: null, Claude(Said("ok")));
        using var job = rig.EnterJob();

        _ = await rig.Ask(RouterRig.Request(ModelPurpose.ConceptScan));
        _ = await rig.Ask(RouterRig.Request(ModelPurpose.Cleanup));

        Assert.Equal(["concept-scan", "map-cleanup"], rig.Calls.Calls.Select(static c => c.Purpose), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_call_outside_a_job_writes_an_event_only_when_it_failed_or_hit_a_limit()
    {
        var rig = new RouterRig(settings: null, Claude(Said("fine"), RouterRig.Limit(), Down()));

        _ = await rig.Ask(RouterRig.Request());
        Assert.Empty(rig.Calls.Calls);

        _ = await rig.Ask(RouterRig.Request());
        _ = await rig.Ask(RouterRig.Request());

        Assert.Equal(2, rig.Calls.Calls.Count);
        Assert.All(rig.Calls.Calls, c => Assert.Null(c.Job));
        Assert.Null(rig.Calls.Calls[0].AnsweredBy);
    }

    // --- not a limit (spec 5.1, 5.4)

    [Fact, Trait("Lane", "unit")]
    public async Task A_failure_that_is_not_a_limit_moves_on_and_is_not_remembered()
    {
        var claude = Claude(new ProviderErrored("exit-1"), Said("later"));
        var codex = Codex(Said("fallback"), Said("never"));
        var rig = new RouterRig(settings: null, claude, codex);
        using var job = rig.EnterJob();

        _ = await rig.Ask(RouterRig.Request());
        var second = (await rig.Ask(RouterRig.Request())).Value;

        Assert.Equal("claude-code", second.Harness);
        Assert.Equal(("claude-code", ModelCallOutcome.Failed, "exit-1"), (rig.Calls.Calls[0].Tries[0].Harness, rig.Calls.Calls[0].Tries[0].Outcome, rig.Calls.Calls[0].Tries[0].Reason));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_successful_answer_that_says_the_limit_string_sets_no_limit()
    {
        var claude = Claude(Said("You've hit your session limit"), Said("again"));
        var rig = new RouterRig(settings: null, claude);
        using var job = rig.EnterJob();

        _ = await rig.Ask(RouterRig.Request());
        _ = await rig.Ask(RouterRig.Request());

        Assert.Equal(2, claude.Asked.Count);
        Assert.All(rig.Calls.Calls, c => Assert.Equal(ModelCallOutcome.Answered, c.Tries[0].Outcome));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Nobody_answering_and_nobody_at_a_limit_is_042_and_never_a_pause()
    {
        var rig = new RouterRig(settings: null, Claude(Down()), Codex(new ProviderErrored("bad-json")));
        using var job = rig.EnterJob();

        var answer = await rig.Ask(RouterRig.Request(ModelPurpose.Adjudication));

        Assert.Equal(ContextErrors.LabelingUnavailable.Code, answer.Error.Code);
        Assert.Empty(rig.Waiting.Waits);
        Assert.Equal(ModelCallOutcome.Failed, rig.Calls.Calls[0].Tries[0].Outcome);
    }

    // --- 240, 241 and the 042 wrapping rule (spec 10)

    [Fact, Trait("Lane", "unit")]
    public async Task No_harness_installed_is_241_and_never_pauses()
    {
        var rig = new RouterRig(settings: null);
        using var job = rig.EnterJob();

        var answer = await rig.Ask(RouterRig.Request(ModelPurpose.Adjudication));

        Assert.Equal(ContextErrors.NoModelHarness.Code, answer.Error.Code);
        Assert.Empty(rig.Waiting.Waits);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_limit_with_no_job_to_park_is_240_at_once_and_names_the_harnesses_and_the_fix()
    {
        var rig = new RouterRig(settings: null, Claude(RouterRig.Limit()), Codex(RouterRig.Limit()));

        var answer = await rig.Ask(RouterRig.Request(ModelPurpose.Adjudication));

        Assert.Equal(ContextErrors.ModelHarnessesUnavailable.Code, answer.Error.Code);
        Assert.Contains("claude-code", answer.Error.Message, StringComparison.Ordinal);
        Assert.Contains("codex", answer.Error.Message, StringComparison.Ordinal);
        Assert.Contains("context.models.adjudication.harnesses", answer.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_job_without_a_park_handle_cannot_pause_either()
    {
        var rig = new RouterRig(settings: null, Claude(RouterRig.Limit()));
        using var job = rig.EnterJob(parkable: false);

        var answer = await rig.Ask(RouterRig.Request(ModelPurpose.Rerank));

        Assert.Equal(ContextErrors.ModelHarnessesUnavailable.Code, answer.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(ModelPurpose.Labeling, "SND-CONTEXT-042")]
    [InlineData(ModelPurpose.Concepts, "SND-CONTEXT-240")]
    [InlineData(ModelPurpose.DecisionExtraction, "SND-CONTEXT-240")]
    [InlineData(ModelPurpose.Cleanup, "SND-CONTEXT-240")]
    public async Task Labeling_gets_the_240_text_under_the_042_code_and_the_other_kinds_get_240(ModelPurpose purpose, string code)
    {
        var rig = new RouterRig(settings: null, Claude(RouterRig.Limit()));

        var answer = await rig.Ask(RouterRig.Request(purpose));

        Assert.Equal(code, answer.Error.Code);
        Assert.Contains("usage limit", answer.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Labeling_with_no_harness_installed_gets_the_241_text_under_the_042_code()
    {
        var rig = new RouterRig(settings: null);

        var answer = await rig.Ask(RouterRig.Request());

        Assert.Equal(ContextErrors.LabelingUnavailable.Code, answer.Error.Code);
        Assert.Contains("install", answer.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    // --- the pause (spec 5.3)

    [Fact, Trait("Lane", "unit")]
    public async Task Every_harness_limited_under_a_job_parks_it_on_model_limit_with_a_line_naming_them()
    {
        var rig = new RouterRig(settings: null, Claude(RouterRig.Limit()), Codex(RouterRig.Limit()));
        using var job = rig.EnterJob();

        var asking = rig.Ask(RouterRig.Request()).AsTask();
        await rig.Waiting.Until(1, Token);

        Assert.Equal(("model-limit", TimeSpan.FromHours(24)), rig.Waiting.Waits[0]);
        Assert.Equal("Paused: claude-code at its usage limit; codex at its usage limit; cursor not installed. Resume when one is back.", Assert.Single(rig.Progress.Lines));
        var called = Assert.Single(rig.Calls.Calls);   // the event is written before the job parks
        Assert.All(called.Tries, t => Assert.Equal(ModelCallOutcome.Limit, t.Outcome));
        Assert.False(asking.IsCompleted);
        _ = rig.Waiting.Expire();
        Assert.Equal(ContextErrors.LabelingUnavailable.Code, (await asking).Error.Code);   // labeling wraps 240 as 042
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Resume_clears_the_set_and_asks_again_from_the_first_harness()
    {
        var claude = Claude(RouterRig.Limit(), Said("back"));
        var codex = Codex(RouterRig.Limit());
        var rig = new RouterRig(settings: null, claude, codex);
        using var job = rig.EnterJob();

        var asking = rig.Ask(RouterRig.Request()).AsTask();
        await rig.Waiting.Until(1, Token);
        _ = rig.Waiting.Resume();
        var answer = (await asking).Value;

        Assert.Equal("claude-code", answer.Harness);
        Assert.Equal(2, claude.Asked.Count);
        Assert.Equal(2, rig.Calls.Calls.Count);   // one event for the pass that parked, one for the pass that answered
        Assert.Equal(ModelCallOutcome.Answered, rig.Calls.Calls[1].Tries[0].Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task One_harness_limited_and_one_failing_parks_the_job_once_instead_of_looping()
    {
        var claude = Claude(RouterRig.Limit(), Said("back"));
        var codex = Codex(new ProviderErrored("exit-1"));
        var rig = new RouterRig(settings: null, claude, codex);
        using var job = rig.EnterJob();

        var asking = rig.Ask(RouterRig.Request(ModelPurpose.Adjudication)).AsTask();
        await rig.Waiting.Until(1, Token);

        _ = Assert.Single(codex.Asked);
        _ = Assert.Single(rig.Waiting.Waits);
        Assert.Contains("codex failed", Assert.Single(rig.Progress.Lines), StringComparison.Ordinal);
        _ = rig.Waiting.Resume();
        Assert.Equal("claude-code", (await asking).Value.Harness);
        Assert.Equal(2, claude.Asked.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Still_limited_after_a_resume_pauses_again()
    {
        var rig = new RouterRig(settings: null, Claude(RouterRig.Limit()));
        using var job = rig.EnterJob();

        var asking = rig.Ask(RouterRig.Request()).AsTask();
        await rig.Waiting.Until(1, Token);
        _ = rig.Waiting.Resume();
        while (rig.Waiting.Waits.Count < 2)
        {
            await Task.Delay(5, Token);   // the first wait leaves the parked list a moment after it is resumed, so count the waits started
        }

        Assert.Equal(2, rig.Waiting.Waits.Count);
        Assert.False(asking.IsCompleted);
        _ = rig.Waiting.Expire();
        Assert.Equal(ContextErrors.ModelHarnessesUnavailable.Code, (await asking).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_twenty_four_hours_running_out_returns_240()
    {
        var rig = new RouterRig(settings: null, Claude(RouterRig.Limit()));
        using var job = rig.EnterJob();

        var asking = rig.Ask(RouterRig.Request(ModelPurpose.Adjudication)).AsTask();
        await rig.Waiting.Until(1, Token);
        _ = rig.Waiting.Expire();

        Assert.Equal(ContextErrors.ModelHarnessesUnavailable.Code, (await asking).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cancelled_job_returns_240_instead_of_throwing()
    {
        var rig = new RouterRig(settings: null, Claude(RouterRig.Limit()));
        using var job = rig.EnterJob();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var asking = rig.Port.Ask(RouterRig.Request(ModelPurpose.Adjudication), cancel.Token).AsTask();
        await rig.Waiting.Until(1, Token);
        await cancel.CancelAsync();

        Assert.Equal(ContextErrors.ModelHarnessesUnavailable.Code, (await asking).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_calls_of_one_job_reaching_the_pause_at_once_park_the_job_once()
    {
        var claude = Claude(RouterRig.Limit(), Said("one"), Said("two"));
        var rig = new RouterRig(settings: null, claude);
        using var job = rig.EnterJob();

        var first = rig.Ask(RouterRig.Request()).AsTask();
        var second = rig.Ask(RouterRig.Request()).AsTask();
        await rig.Waiting.Until(1, Token);
        await Task.Delay(50, Token);
        _ = Assert.Single(rig.Waiting.Waits);   // the second waits behind the first's lock
        _ = rig.Waiting.Resume();

        Assert.True((await first).IsSuccess);
        Assert.True((await second).IsSuccess);
        _ = Assert.Single(rig.Waiting.Waits);
    }

    private static string JobId() => "JOB-" + new string('a', 32);
}
