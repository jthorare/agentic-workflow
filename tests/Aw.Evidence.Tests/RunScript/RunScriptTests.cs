using Aw.Evidence.Core;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Images;
using Aw.Evidence.Core.Judging;
using Aw.Evidence.Core.Lint;
using Aw.Evidence.Core.Runs;
using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Core.Steps;
using Aw.Evidence.Core.Usage;
using Aw.Evidence.Core.Visual;
using Aw.Evidence.Testing;
using Aw.Kernel.Json;

namespace Aw.Evidence.Tests.RunScript;

public sealed class RunScriptTests
{
    private static UiScript OpenMenu(params ViewportKind[] viewports) => A.Script()
        .WithStep(ScriptBuilder.Goto("/hospitals"))
        .WithStep(ScriptBuilder.Click("open-menu", new TextVisible("Menu")))
        .In(viewports.Length > 0 ? viewports : [ViewportKind.Desktop])
        .Build();

    private static RunScriptHarness WithMenu()
    {
        var harness = new RunScriptHarness();
        _ = harness.Page.TestIds.Add("open-menu");
        harness.Page.OnClick["testId:open-menu"] = static p => p.Texts.Add("Menu");
        return harness;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunScript_writes_a_summary_with_every_step_passed()
    {
        var harness = WithMenu();
        var bytes = harness.WithScript(OpenMenu());

        var result = await harness.Run(A.RunScriptRequest().Build());

        Assert.True(result.IsSuccess);
        var summary = result.Value.Summary;
        Assert.True(result.Value.Passed);
        Assert.Equal(RunSummary.SchemaName, summary.Schema);
        Assert.Equal("run-1", summary.RunId.Value);
        Assert.Equal("http://localhost:3000", summary.Host);
        Assert.Equal(ContentHash.Sha256Hex(bytes), summary.ScriptSha256);
        Assert.Equal([StepStatus.Passed, StepStatus.Passed], summary.Steps.Select(static s => s.Status));
        Assert.Equal(["/runs/run-1/desktop-1-goto.png", "/runs/run-1/desktop-2-click.png"], summary.Steps.Select(static s => s.Screenshot), StringComparer.Ordinal);
        Assert.All(summary.Steps, s => Assert.True(harness.Files.Files.ContainsKey(s.Screenshot)));
        Assert.Equal(["/runs/run-1/desktop-trace.zip"], summary.Evidence.Traces);
        Assert.Equal(AwJson.Serialize(summary, TestJson.Options), harness.Files.Files["/runs/run-1/summary.json"]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunScript_holds_both_locks_for_the_run_and_releases_them_widest_last()
    {
        var harness = WithMenu();
        _ = harness.WithScript(OpenMenu());

        _ = await harness.Run(A.RunScriptRequest().Build());

        Assert.Equal(["acquire QaStack", "acquire HeavyJob", "release HeavyJob", "release QaStack"], harness.Locks.Events);
        Assert.All(harness.Browser.Sessions, static s => Assert.True(s.Disposed));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunScript_records_a_failed_expectation_and_still_runs_every_step()
    {
        var harness = new RunScriptHarness { Judge = new FakeJudge() };
        _ = harness.Page.TestIds.Add("open-menu");
        _ = harness.WithScript(A.Script()
            .WithStep(ScriptBuilder.Click("open-menu", new TextVisible("Menu")))
            .WithStep(ScriptBuilder.Goto("/hospitals"))
            .Build());

        var result = await harness.Run(A.RunScriptRequest().Build());

        Assert.False(result.Value.Passed);
        Assert.Equal([StepStatus.Failed, StepStatus.Passed], result.Value.Summary.Steps.Select(static s => s.Status));
        _ = Assert.Single(harness.Judge.CritiqueRequests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunScript_records_the_planning_call_and_a_failed_critique_as_unchecked()
    {
        var harness = WithMenu();
        harness.Judge = new FakeJudge();
        _ = harness.WithScript(A.Script().WithStep(ScriptBuilder.Goto("/hospitals"))
            .WithPlanning(new PlanningMeta(Pr: "42", Model: "planner", ElapsedMs: 900, InputTokens: 10, OutputTokens: null)).Build());

        var summary = (await harness.Run(A.RunScriptRequest().Build())).Value.Summary;

        Assert.Equal(Verdict.Unchecked, summary.Visual.Verdict);
        Assert.Equal("42", summary.Pr);
        Assert.Equal([InvocationPhase.Planning, InvocationPhase.VisualCritique], summary.Invocations.Select(static i => i.Phase));
        Assert.Null(summary.Invocations[0].OutputTokens);
        Assert.False(summary.Invocations[1].Ok);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunScript_without_a_judge_stays_unchecked_and_records_no_model_call()
    {
        var harness = WithMenu();
        _ = harness.WithScript(A.Script().WithStep(ScriptBuilder.Goto("/hospitals")).Build());

        var summary = (await harness.Run(A.RunScriptRequest().Build())).Value.Summary;

        Assert.Equal(Verdict.Unchecked, summary.Visual.Verdict);
        Assert.Empty(summary.Invocations);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunScript_is_unchanged_without_a_model_when_the_pixels_match_the_baseline()
    {
        var harness = WithMenu();
        var image = harness.Codec.Encode(RgbaImage.Filled(8, 8, 0xFFFFFFFF));
        harness.Page.Screenshot = image;
        _ = harness.Files.With("/baselines/main.png", image);
        _ = harness.WithScript(OpenMenu());

        var summary = (await harness.Run(A.RunScriptRequest().WithBaseline("/baselines/main.png").Build())).Value.Summary;

        Assert.Equal(Verdict.Unchanged, summary.Visual.Verdict);
        Assert.Empty(harness.Judge.CritiqueRequests);
        Assert.Equal(0, summary.Visual.DiffScore);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunScript_reuses_a_cached_verdict_for_the_same_build_and_pixels()
    {
        var harness = WithMenu();
        harness.Judge = new FakeJudge().Critiques(new VisualVerdict(VisualDecision.LooksRight, [], Usage: null));
        _ = harness.WithScript(OpenMenu());
        var request = A.RunScriptRequest().WithAppBuild("abc123").WithCache("/cache/verdicts.json");

        var first = (await harness.Run(request.Build())).Value.Summary;
        var second = (await harness.Run(request.In("/runs/run-2").Build())).Value.Summary;

        Assert.Equal(Verdict.LooksRight, first.Visual.Verdict);
        Assert.Equal(Verdict.LooksRight, second.Visual.Verdict);
        _ = Assert.Single(harness.Judge.CritiqueRequests);
        Assert.True(second.Invocations.Single().CacheHit);
        Assert.True(harness.Cache.Saved.ContainsKey("/cache/verdicts.json"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunScript_never_reuses_a_verdict_formed_before_jev_images_were_turned_off()
    {
        var harness = WithMenu();
        harness.Judge = new FakeJudge { JevImages = true }.Critiques(
            new VisualVerdict(VisualDecision.LooksRight, [], Usage: null), new VisualVerdict(VisualDecision.LooksOff, ["text only"], Usage: null));
        _ = harness.WithScript(OpenMenu());
        var request = A.RunScriptRequest().WithAppBuild("abc123").WithCache("/cache/verdicts.json");

        _ = await harness.Run(request.Build());
        harness.Judge.JevImages = false;   // judge config set providers jev-images false
        var after = (await harness.Run(request.In("/runs/run-2").Build())).Value.Summary;

        Assert.Equal(Verdict.LooksOff, after.Visual.Verdict);
        Assert.Equal(2, harness.Judge.CritiqueRequests.Count);
        Assert.False(after.Invocations.Single().CacheHit);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunScript_never_reuses_an_earlier_runs_verdict_without_an_app_build_even_in_the_same_directory()
    {
        var harness = WithMenu();
        harness.Judge = new FakeJudge().Critiques(
            new VisualVerdict(VisualDecision.LooksRight, [], Usage: null), new VisualVerdict(VisualDecision.LooksOff, ["new build"], Usage: null));
        _ = harness.WithScript(OpenMenu());
        var request = A.RunScriptRequest().Build();   // no --app-build: identical pixels can hide a new build

        _ = await harness.Run(request);
        var rerun = (await harness.Run(request)).Value.Summary;   // the same run directory, so the same run name and cache file

        Assert.Equal(Verdict.LooksOff, rerun.Visual.Verdict);
        Assert.Equal(2, harness.Judge.CritiqueRequests.Count);
        Assert.False(rerun.Invocations.Single().CacheHit);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunScript_lints_the_final_page_of_each_viewport()
    {
        var harness = WithMenu();
        harness.Page.Snapshot = new PageSnapshot(
            [new PageElement("div#wide", new Box(0, 0, 500, 20), "Wide", new ElementStyle(TextOverflow: null, Overflow: null, Color: null), HasHoverState: true, HasFocusState: true)],
            new ViewportSize(375, 812));
        _ = harness.WithScript(OpenMenu(ViewportKind.Desktop, ViewportKind.Phone));

        var summary = (await harness.Run(A.RunScriptRequest().Build())).Value.Summary;

        Assert.Equal([ViewportKind.Desktop, ViewportKind.Phone], summary.LintFindings.Select(static f => f.Viewport));
        Assert.All(summary.LintFindings, static f => Assert.Equal(LintRule.PhoneOverflow, f.Rule));
        Assert.Equal(4, summary.Steps.Count);
        Assert.Equal(["desktop", "phone"], harness.Browser.Sessions.Select(static s => s.Options.Viewport.W == 375 ? "phone" : "desktop"), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunScript_presents_the_configured_user_agent()
    {
        var harness = WithMenu();
        harness.Settings.UserAgent = "Agent/1";
        _ = harness.WithScript(OpenMenu());

        _ = await harness.Run(A.RunScriptRequest().Build());

        Assert.Equal("Agent/1", harness.Browser.Sessions.Single().Options.UserAgentOverride);
        Assert.Equal("/runs/run-1", harness.Browser.Sessions.Single().Options.RecordDirectory);
    }
}
