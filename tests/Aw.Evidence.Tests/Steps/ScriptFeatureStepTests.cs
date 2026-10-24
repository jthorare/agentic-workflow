using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Judging;
using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Core.Steps;
using Aw.Evidence.Core.Usage;
using Aw.Evidence.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Evidence.Tests.Steps;

/// <summary>The step runner over the script features of cutover gap G-a (spec §3.3): scroll, drag, press, indexed and prefixed targets, the two new expectations.</summary>
public sealed class ScriptFeatureStepTests
{
    private static readonly BrowserSessionOptions _options = new(new ResolvedHost("http://localhost:3000", HostKind.Local), new ViewportSize(1280, 800), RecordDirectory: null, UserAgentOverride: null);
    private static readonly ExpectedState _done = new TextVisible("Done");

    private static FakeBrowserSession Session(Action<FakePage> arrange)
    {
        var page = new FakePage();
        arrange(page);
        return new FakeBrowserSession(page, _options);
    }

    private static async Task<StepStatus> Run(FakeBrowserSession session, ScriptStep step, FakeJudge? judge = null) =>
        await new StepRunner(judge ?? FakeJudge.Unavailable, new FakeTimeProvider()).RunAsync(session, step, StepRunOptions.Run, new InvocationLog(), CancellationToken.None);

    [Fact, Trait("Lane", "unit")]
    public async Task A_scroll_defaults_to_the_end_and_passes_when_its_expectation_holds()
    {
        var session = Session(static p =>
        {
            _ = p.TestIds.Add("timeline");
            p.OnScroll["testId:timeline"] = static page => page.Texts.Add("Done");
        });

        var status = await Run(session, new ScriptStep(StepAction.Scroll, "timeline", _done));

        Assert.Equal(StepStatus.Passed, status);
        Assert.Equal(["scroll testId:timeline end"], session.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_scroll_of_a_missing_container_is_broken_and_asks_no_judge()
    {
        var judge = new FakeJudge().Repairs(new SelectorRepair(RepairDecision.Repaired, 0, Usage: null));

        var status = await Run(Session(static _ => { }), new ScriptStep(StepAction.Scroll, "timeline", _done, "top"), judge);

        Assert.Equal(StepStatus.Broken, status);
        Assert.Empty(judge.RepairRequests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_drag_carries_its_offsets_and_alt_to_the_session()
    {
        var gestures = new List<DragGesture>();
        var session = Session(p =>
        {
            _ = p.TestIds.Add("card-1");
            _ = p.TestIds.Add("lane-2");
            p.OnDrag["testId:card-1>testId:lane-2"] = (page, gesture) =>
            {
                gestures.Add(gesture);
                _ = page.Texts.Add("Done");
            };
        });
        var step = new ScriptStep(StepAction.Drag, _done, "card-1", to: "lane-2", grabOffset: new Offset(4, 8), dropOffset: new Offset(0, 12), holdAlt: true);

        var status = await Run(session, step);

        Assert.Equal(StepStatus.Passed, status);
        Assert.Equal([new DragGesture(new Offset(4, 8), new Offset(0, 12), HoldAlt: true)], gestures);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_drag_with_a_missing_end_is_broken_and_never_repaired()
    {
        var judge = new FakeJudge().Repairs(new SelectorRepair(RepairDecision.Repaired, 0, Usage: null));
        var session = Session(static p => p.TestIds.Add("card-1"));

        var status = await Run(session, new ScriptStep(StepAction.Drag, _done, "card-1", to: "lane-2"), judge);

        Assert.Equal(StepStatus.Broken, status);
        Assert.Empty(judge.RepairRequests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_press_goes_to_the_target_or_to_whatever_has_focus()
    {
        var session = Session(static p =>
        {
            _ = p.TestIds.Add("tab-1");
            p.OnKey = static (page, key) => page.Texts.Add(key == "ArrowRight" ? "Done" : "other");
        });

        var onTarget = await Run(session, new ScriptStep(StepAction.Press, _done, "tab-1", key: "ArrowRight"));
        var focused = await Run(session, new ScriptStep(StepAction.Press, _done, key: "ArrowRight"));

        Assert.Equal([StepStatus.Passed, StepStatus.Passed], [onTarget, focused]);
        Assert.Equal(["press ArrowRight on testId:tab-1", "press ArrowRight"], session.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_press_on_a_missing_target_sends_no_key_and_is_broken()
    {
        var keys = new List<string>();
        var session = Session(p => p.OnKey = (_, key) => keys.Add(key));

        var status = await Run(session, new ScriptStep(StepAction.Press, _done, "tab-1", key: "Enter"));

        Assert.Equal(StepStatus.Broken, status);
        Assert.Empty(keys);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_index_picks_the_nth_of_a_repeated_test_id()
    {
        var session = Session(static p =>
        {
            _ = p.TestIds.Add("row-open");
            p.Counts["testId:row-open"] = 3;
            p.OnClick["testId:row-open[1]"] = static page => page.Texts.Add("Done");
        });

        var picked = await Run(session, new ScriptStep(StepAction.Click, _done, "row-open", index: 1));
        var ambiguous = await Run(session, new ScriptStep(StepAction.Click, _done, "row-open"));

        Assert.Equal([StepStatus.Passed, StepStatus.Broken], [picked, ambiguous]);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("role:option|Day#2", "role:option|Day#2")]
    [InlineData("role:button|Save", "role:button|Save")]
    [InlineData("text:Day#2", "exact:Day#2")]
    public async Task A_prefixed_target_clicks_by_role_or_exact_text(string target, string key)
    {
        var session = Session(p =>
        {
            p.Counts[key.Split('#')[0]] = 2;
            _ = key.StartsWith("role:", StringComparison.Ordinal) ? p.Roles.Add(key.Split('#')[0][5..]) : p.Texts.Add("Day");
            p.OnClick[key] = static page => page.Texts.Add("Done");
        });
        var hasNth = target.Contains('#', StringComparison.Ordinal);

        var status = await Run(session, new ScriptStep(StepAction.Click, _done, target));

        Assert.Equal(hasNth ? StepStatus.Passed : StepStatus.Broken, status);   // two matches without '#n' are ambiguous
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missed_prefixed_target_is_the_planners_to_fix_so_no_judge_is_asked()
    {
        var judge = new FakeJudge().Repairs(new SelectorRepair(RepairDecision.Repaired, 0, Usage: null));
        var session = Session(static p => p.Candidates.Add(new Candidate(0, "button", "Save", TestId.From("save"), "Save")));

        var status = await Run(session, new ScriptStep(StepAction.Click, _done, "role:button|Store"), judge);

        Assert.Equal(StepStatus.Broken, status);
        Assert.Empty(judge.RepairRequests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Url_search_holds_for_one_query_parameter_and_testid_absent_looks_once()
    {
        var session = Session(static p =>
        {
            _ = p.TestIds.Add("filter-night");
            p.OnClick["testId:filter-night"] = static page => page.Query["timeFilter"] = "Night";
        });

        var searched = await Run(session, new ScriptStep(StepAction.Click, "filter-night", new UrlSearch("timeFilter", "Night")));
        var wrong = await Run(session, new ScriptStep(StepAction.Click, "filter-night", new UrlSearch("timeFilter", "Day")));
        var absent = await Run(session, new ScriptStep(StepAction.Click, "filter-night", new TestIdAbsent(TestId.From("drag-ghost"))));
        var present = await Run(session, new ScriptStep(StepAction.Click, "filter-night", new TestIdAbsent(TestId.From("filter-night"))));

        Assert.Equal([StepStatus.Passed, StepStatus.Failed, StepStatus.Passed, StepStatus.Failed], [searched, wrong, absent, present]);
    }
}
