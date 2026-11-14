using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #3683 Compare (frame wf-compare): two ticked runs side by side, step by step. Each step shows both runs' status, duration and output, a differing field is marked, a failed step reads its error as one line,
/// a step one run never reached says it did not run, and the header says how many steps differ. The clock is 2026-01-01 00:05 UTC.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RunCompareTests
{
    private const string RunA = "morning-check@2:5";
    private const string RunB = "morning-check@1:4";

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonObject Step(string node, string status, JsonNode? output = null, string? error = null, double seconds = 1, JsonNode? input = null) =>
        new() { ["node"] = node, ["itemIndex"] = 0, ["status"] = status, ["input"] = input, ["output"] = output, ["truncated"] = false, ["error"] = error, ["agent"] = null, ["raw"] = null, ["seconds"] = seconds };

    private static JsonObject Run(string id, int revision, string status, string trigger, TimeSpan ago, double seconds, string? error, params JsonObject[] nodes) => new()
    {
        ["id"] = id, ["slug"] = Slug, ["revision"] = revision, ["status"] = status, ["trigger"] = new JsonObject { ["kind"] = trigger }, ["outputs"] = null, ["error"] = error, ["itemId"] = null,
        ["startedAt"] = (Now - ago).ToString("O", System.Globalization.CultureInfo.InvariantCulture), ["endedAt"] = status == "running" ? null : (Now - ago + TimeSpan.FromSeconds(seconds)).ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        ["nodes"] = new JsonArray([.. nodes.Select(n => (JsonNode?)n)]),
    };

    private async Task<WorkflowsPageViewModel> TwoRuns(JsonObject a, JsonObject b)
    {
        var workflow = Golden("workflows-slug", "found");
        workflow["spec"]!["nodes"] = new JsonArray(
            new JsonObject { ["id"] = "start", ["type"] = "manual", ["config"] = new JsonObject() },
            new JsonObject { ["id"] = "sum", ["type"] = "code", ["config"] = new JsonObject() },
            new JsonObject { ["id"] = "tell", ["type"] = "notify", ["config"] = new JsonObject() });
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, workflow.ToJsonString());
        _f.Server.WorkflowRunsAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, new JsonObject
        {
            ["slug"] = Slug,
            ["runs"] = new JsonArray(
                new JsonObject { ["id"] = RunA, ["revision"] = 2, ["status"] = "failed", ["trigger"] = "cli", ["startedAt"] = a["startedAt"]!.DeepClone(), ["endedAt"] = a["endedAt"]!.DeepClone(), ["error"] = "x" },
                new JsonObject { ["id"] = RunB, ["revision"] = 1, ["status"] = "failed", ["trigger"] = "manual", ["startedAt"] = b["startedAt"]!.DeepClone(), ["endedAt"] = b["endedAt"]!.DeepClone(), ["error"] = "x" }),
        }.ToJsonString());
        _f.Server.WorkflowRunAnswers[RunA] = new RouteAnswer(System.Net.HttpStatusCode.OK, a.ToJsonString());
        _f.Server.WorkflowRunAnswers[RunB] = new RouteAnswer(System.Net.HttpStatusCode.OK, b.ToJsonString());
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        foreach (var row in page.Runs.Rows)
        {
            row.IsSelected = true;
        }

        await page.OpenCompare(Token);
        return page;
    }

    private static JsonObject Manual(string by) => Step("start", "succeeded", new JsonObject { ["payload"] = new JsonObject { ["kind"] = "manual", ["by"] = by } }, seconds: 0.4);

    [Fact]
    public async Task Compare_opens_the_two_runs_step_by_step_with_each_steps_name_and_kind_and_both_headers()
    {
        var a = Run(RunA, 2, "failed", "cli", TimeSpan.FromHours(2), 10, "tell failed: the notification was not delivered", Manual("human:cli"), Step("sum", "succeeded", new JsonObject { ["result"] = 3 }, seconds: 5.2), Step("tell", "failed", error: "The notification was not delivered", seconds: 4.4));
        var b = Run(RunB, 1, "failed", "manual", TimeSpan.FromHours(30), 10, "sum failed: code runner not found (SND-CODE-005)", Manual("human:ui"), Step("sum", "failed", error: "sum failed: code runner not found (SND-CODE-005)", seconds: 9.6), Step("tell", "skipped", error: "sum failed", seconds: 0));

        var page = await TwoRuns(a, b);

        var compare = page.Compare;
        Assert.True(page.IsCompare);
        Assert.Equal([("start", "manual"), ("sum", "code"), ("tell", "notify")], compare.Steps.Select(s => (s.Name, s.Kind)));
        Assert.Equal(("Run A · Wed 31 Dec 2025 · 22:05", "r2 · 10 s · From the CLI", "Failed", "tell failed: the notification was not delivered"), (compare.A.Title, compare.A.Facts, compare.A.ChipWord, compare.A.Result));
        Assert.Equal(("Run B · Tue 30 Dec 2025 · 18:05", "r1 · 10 s · By hand"), (compare.B.Title, compare.B.Facts));
    }

    [Fact]
    public async Task A_differing_output_field_is_marked_and_the_lines_that_match_are_not()
    {
        var a = Run(RunA, 2, "failed", "cli", TimeSpan.FromHours(2), 10, "e", Manual("human:cli"), Step("sum", "succeeded", new JsonObject { ["result"] = 3 }), Step("tell", "failed", error: "e"));
        var b = Run(RunB, 1, "failed", "manual", TimeSpan.FromHours(30), 10, "e", Manual("human:ui"), Step("sum", "succeeded", new JsonObject { ["result"] = 3 }), Step("tell", "failed", error: "e"));

        var page = await TwoRuns(a, b);

        var start = page.Compare.Steps[0];
        Assert.Equal(["\"by\": \"human:cli\""], start.A.Lines.Where(l => l.Differs).Select(l => l.Text.Trim()));
        Assert.Equal(["\"by\": \"human:ui\""], start.B.Lines.Where(l => l.Differs).Select(l => l.Text.Trim()));
        Assert.Contains(start.A.Lines, l => !l.Differs && l.Text.Contains("\"kind\": \"manual\"", StringComparison.Ordinal));
        Assert.Equal(("differs in 1 of 3 steps", false, false), (page.Compare.SummaryText, page.Compare.Steps[1].Differs, page.Compare.Steps[2].Differs));
    }

    [Fact]
    public async Task A_failed_step_reads_its_error_as_one_line_and_a_skipped_one_says_it_did_not_run()
    {
        var a = Run(RunA, 1, "failed", "cli", TimeSpan.FromHours(2), 10, "e", Manual("x"), Step("sum", "succeeded", new JsonObject { ["result"] = 3 }), Step("tell", "failed", error: "The notification was not delivered"));
        var b = Run(RunB, 1, "failed", "manual", TimeSpan.FromHours(3), 10, "e", Manual("x"), Step("sum", "failed", error: "sum failed: code runner not found (SND-CODE-005)"), Step("tell", "skipped", error: "sum failed"));

        var page = await TwoRuns(a, b);

        var (sum, tell) = (page.Compare.Steps[1], page.Compare.Steps[2]);
        Assert.Equal(("Failed", "sum failed: code runner not found (SND-CODE-005)", true), (sum.B.StatusWord, sum.B.Message, sum.B.MessageDiffers));
        Assert.Equal(("Skipped", "Did not run: sum failed"), (tell.B.StatusWord, tell.B.Message));
        Assert.Equal("The notification was not delivered", tell.A.Message);
    }

    [Fact]
    public async Task Two_identical_runs_say_they_match_and_mark_nothing()
    {
        JsonObject Same(string id, int hours) => Run(id, 1, "succeeded", "manual", TimeSpan.FromHours(hours), 10, null, Manual("human:ui"), Step("sum", "succeeded", new JsonObject { ["result"] = 3 }), Step("tell", "succeeded", new JsonObject()));

        var page = await TwoRuns(Same(RunA, 2), Same(RunB, 3));

        Assert.Equal(("the two runs match", 0), (page.Compare.SummaryText, page.Compare.Steps.Count(s => s.Differs)));
        Assert.All(page.Compare.Steps.SelectMany(s => s.A.Lines), l => Assert.False(l.Differs));
    }

    [Fact]
    public async Task A_step_only_one_run_reached_says_so_in_the_other_and_differs()
    {
        var a = Run(RunA, 2, "succeeded", "manual", TimeSpan.FromHours(2), 10, null, Manual("x"), Step("sum", "succeeded", new JsonObject { ["result"] = 3 }), Step("tell", "succeeded", new JsonObject()));
        var b = Run(RunB, 1, "succeeded", "manual", TimeSpan.FromHours(3), 10, null, Manual("x"), Step("sum", "succeeded", new JsonObject { ["result"] = 3 }));

        var page = await TwoRuns(a, b);

        var tell = page.Compare.Steps[2];
        Assert.Equal((false, "Not in this run", true), (tell.B.Present, tell.B.Message, tell.Differs));
    }

    [Fact]
    public async Task A_run_still_running_is_named_in_the_summary_line()
    {
        var a = Run(RunA, 1, "running", "manual", TimeSpan.FromMinutes(1), 0, null, Manual("x"));
        var b = Run(RunB, 1, "succeeded", "manual", TimeSpan.FromHours(3), 10, null, Manual("x"), Step("sum", "succeeded", new JsonObject { ["result"] = 3 }));

        var page = await TwoRuns(a, b);

        Assert.Equal("Run A is still running; its steps so far are shown", page.Compare.RunningNote);
    }

    [Fact]
    public async Task One_ticked_run_or_three_do_not_compare_and_the_hint_says_what_to_do()
    {
        var page = await _f.Shown(Token);
        _f.Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        await page.OpenWorkflow(Slug, Token);

        page.Runs.Rows[0].IsSelected = true;
        Assert.Equal("Tick one more run to compare", page.Runs.CompareHint);
        page.Runs.Rows[1].IsSelected = true;
        Assert.Equal(string.Empty, page.Runs.CompareHint);
        page.Runs.Rows[2].IsSelected = true;
        Assert.Equal("Compare takes two runs: untick one", page.Runs.CompareHint);
        await page.OpenCompare(Token);
        Assert.False(page.IsCompare);
    }

    [Fact]
    public async Task Back_from_Compare_returns_to_the_runs_with_the_two_ticks_kept()
    {
        var a = Run(RunA, 1, "succeeded", "manual", TimeSpan.FromHours(2), 10, null, Manual("x"));
        var b = Run(RunB, 1, "succeeded", "manual", TimeSpan.FromHours(3), 10, null, Manual("x"));
        var page = await TwoRuns(a, b);

        page.Back();

        Assert.Equal((true, false, 2), (page.IsRuns, page.IsCompare, page.Runs.SelectedCount));
    }
}
