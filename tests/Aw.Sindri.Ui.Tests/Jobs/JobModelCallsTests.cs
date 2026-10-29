using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Jobs;
using static Aw.Sindri.Ui.Tests.Jobs.JobsFixture;

namespace Aw.Sindri.Ui.Tests.Jobs;

/// <summary>Model calls on the Jobs page and the job sheet (context model routing spec §8.2): the row's model line and its variants, the Resume button, the sheet's list of tries.</summary>
[Lane(TestLane.Unit)]
public sealed class JobModelCallsTests
{
    private readonly JobsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonObject Totals(int calls, int failed, long input, long output, int fellBack, params (string Harness, string Model, int Count)[] answered) => new()
    {
        ["calls"] = calls,
        ["failedCalls"] = failed,
        ["inputTokens"] = input,
        ["outputTokens"] = output,
        ["answeredBy"] = new JsonArray([.. answered.Select(static a => (JsonNode)new JsonObject { ["harness"] = a.Harness, ["model"] = a.Model, ["count"] = a.Count })]),
        ["fellBack"] = fellBack,
    };

    private async Task<JobRowViewModel> RowWith(JsonNode? models, string? waitingOn = null, string? waitingLine = null, string state = "running")
    {
        var list = Golden("jobs", "running");
        var job = list["jobs"]![0]!;
        job["kind"] = "BuildContextMap";
        job["state"] = state;
        job["models"] = models;
        job["waitingOn"] = waitingOn;
        job["waitingLine"] = waitingLine;
        _f.ServeList(list);
        return Assert.Single((await _f.Shown(Token)).Rows);
    }

    [Fact]
    public async Task A_job_that_made_no_call_has_no_model_line()
    {
        var row = await RowWith(models: null);

        Assert.False(row.HasModelText);
        Assert.Equal(string.Empty, row.ModelText);
    }

    [Fact]
    public async Task The_line_names_the_harness_and_model_that_answered_with_the_calls_and_the_tokens()
    {
        var row = await RowWith(Totals(3, 0, 3000, 1100, 0, ("codex", "gpt-6-luna", 3)));

        Assert.True(row.HasModelText);
        Assert.Equal("Models: codex gpt-6-luna 3 calls · 4.1k tokens (input plus output)", row.ModelText);
    }

    [Fact]
    public async Task One_call_is_a_call_and_a_fallback_is_counted_at_the_end()
    {
        var row = await RowWith(Totals(1, 0, 700, 50, 1, ("codex", "gpt-6-luna", 1)));

        Assert.Equal("Models: codex gpt-6-luna 1 call · 750 tokens (input plus output) (1 fell back)", row.ModelText);
    }

    [Fact]
    public async Task At_most_two_harness_and_model_pairs_are_named_and_the_rest_are_counted()
    {
        var row = await RowWith(Totals(6, 0, 10000, 2300, 0, ("claude-code", "haiku", 2), ("codex", "gpt-6-luna", 3), ("cursor", "auto", 1)));

        Assert.Equal("Models: claude-code haiku 2 calls, codex gpt-6-luna 3 calls +1 more · 12.3k tokens (input plus output)", row.ModelText);
    }

    [Fact]
    public async Task When_no_call_answered_the_line_says_how_many_failed_and_names_no_tokens()
    {
        var row = await RowWith(Totals(3, 3, 0, 0, 0));

        Assert.Equal("Models: no call answered (3 failed)", row.ModelText);
    }

    [Fact]
    public async Task A_job_paused_on_a_limit_reads_waiting_shows_the_progress_line_and_offers_Resume()
    {
        var line = "Paused: claude-code at its usage limit; codex at its usage limit. Resume when one is back.";

        var row = await RowWith(Totals(2, 0, 100, 50, 0, ("codex", "gpt-6-luna", 2)), "model-limit", line, state: "waiting");

        Assert.Equal(("○ Waiting", line, true), (row.ChipText, row.ModelText, row.HasResume));
        Assert.Equal(("Resume", "Resume Build the context map"), (row.ResumeText, row.ResumeName));
    }

    [Fact]
    public async Task A_paused_job_with_no_made_call_still_shows_why_it_waits()
    {
        var row = await RowWith(models: null, "model-limit", waitingLine: null, state: "waiting");

        Assert.True(row.HasModelText);
        Assert.Equal(Labels.JobModelPausedFallback, row.ModelText);
        Assert.True(row.HasResume);
    }

    [Fact]
    public async Task A_job_waiting_on_something_else_has_no_Resume()
    {
        var row = await RowWith(models: null, "index-login", "Waiting for the Onyx login.", state: "waiting");

        Assert.False(row.HasResume);
        Assert.False(row.HasModelText);
    }

    [Fact]
    public async Task The_rows_accessible_name_ends_with_the_model_line()
    {
        var row = await RowWith(Totals(3, 0, 3000, 1100, 0, ("codex", "gpt-6-luna", 3)));

        Assert.EndsWith(", Models: codex gpt-6-luna 3 calls · 4.1k tokens (input plus output)", row.Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resume_sends_the_resume_and_reads_the_list_again()
    {
        var row = await RowWith(models: null, "model-limit", "Paused: x. Resume when one is back.", state: "waiting");
        var reads = _f.Server.JobsCalls;

        await row.Resume(Token);

        Assert.Equal([row.Id], _f.Server.ResumedJobs);
        Assert.True(_f.Server.JobsCalls > reads);
    }

    [Fact]
    public async Task A_Resume_that_finds_the_job_no_longer_waiting_only_reads_again()
    {
        var row = await RowWith(models: null, "model-limit", "Paused: x. Resume when one is back.", state: "waiting");
        _f.Server.ResumeJobAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.Conflict, "SND-JOB-003", "The job is not waiting for a model harness.");
        var reads = _f.Server.JobsCalls;

        await row.Resume(Token);

        Assert.True(_f.Server.JobsCalls > reads);
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(999, "999")]
    [InlineData(1000, "1k")]
    [InlineData(4100, "4.1k")]
    [InlineData(12345, "12.3k")]
    [InlineData(150000, "150k")]
    [InlineData(1200000, "1.2M")]
    public void Tokens_read_short(long tokens, string expected) => Assert.Equal(expected, ModelLines.Tokens(tokens));

    // The sheet.

    private static JsonObject Try(string harness, string model, string outcome, string? reason = null, int ms = 1000) =>
        new() { ["harness"] = harness, ["model"] = model, ["outcome"] = outcome, ["reason"] = reason, ["ms"] = ms, ["source"] = "builtin" };

    private static JsonObject Call(string id, string at, string kind, int input, int output, string[]? answered, JsonNode[] tries, int? items = null, JsonNode? how = null) => new()
    {
        ["callId"] = id,
        ["job"] = Id,
        ["purpose"] = kind,
        ["kind"] = kind,
        ["tries"] = new JsonArray(tries),
        ["answeredBy"] = answered is null ? null : new JsonObject { ["harness"] = answered[0], ["model"] = answered[1] },
        ["inputTokens"] = input,
        ["outputTokens"] = output,
        ["tokenBasis"] = "reported",
        ["items"] = items,
        ["how"] = how,
        ["startedAt"] = at,
        ["ms"] = 2000,
    };

    private async Task<JobSheetViewModel> SheetWith(int count, string state = "running", string? waitingOn = null, IReadOnlyList<string>? progress = null, params JsonNode[] calls)
    {
        var job = Golden("jobs-id", "running");
        job["state"] = state;
        job["waitingOn"] = waitingOn;
        job["modelCalls"] = new JsonArray(calls);
        job["modelCallCount"] = count;
        job["progress"] = new JsonArray([.. (progress ?? []).Select(static (l, n) => (JsonNode)new JsonObject { ["round"] = n + 1, ["line"] = l, ["at"] = "2026-01-01T00:00:00.0000000Z" })]);
        _f.Server.JobAnswers[Id] = new RouteAnswer(System.Net.HttpStatusCode.OK, job.ToJsonString());
        var sheet = _f.Sheet();
        await sheet.Open(Url, Id, Token);
        return sheet;
    }

    [Fact]
    public async Task The_sheet_lists_each_try_as_its_own_row_in_call_order()
    {
        var sheet = await SheetWith(2, calls:
        [
            Call("c1", "2026-01-01T00:02:11Z", "labeling", 3000, 1100, ["codex", "gpt-6-luna"], [Try("claude-code", "haiku", "limit", "limit"), Try("codex", "gpt-6-luna", "answered")]),
            Call("c2", "2026-01-01T00:02:40Z", "labeling", 0, 0, answered: null, [Try("claude-code", "haiku", "failed", "exit-1", 400)]),
        ]);

        Assert.True(sheet.HasModelCalls);
        Assert.Equal(
            [
                ("00:02:11", "labeling", "claude-code haiku", string.Empty, "limit", "00:02:11, labeling, claude-code haiku, limit"),
                ("00:02:12", "labeling", "codex gpt-6-luna", "4.1k tokens", "answered", "00:02:12, labeling, codex gpt-6-luna, 4.1k tokens, answered"),
                ("00:02:40", "labeling", "claude-code haiku", "exit-1", "failed", "00:02:40, labeling, claude-code haiku, exit-1, failed"),
            ],
            sheet.ModelCalls.Select(static r => (r.Time, r.Kind, r.Who, r.Detail, r.Outcome, r.Name)));
    }

    [Fact]
    public async Task A_skipped_try_says_why_and_a_time_from_another_day_carries_its_date()
    {
        var sheet = await SheetWith(1, calls: [Call("c1", "2025-12-31T23:59:00Z", "labeling", 10, 5, ["codex", "gpt-6-luna"], [Try("claude-code", "haiku", "skipped", "limited", 0), Try("codex", "gpt-6-luna", "answered")])]);

        Assert.Equal(("2025-12-31 23:59:00", "limited earlier in this job"), (sheet.ModelCalls[0].Time, sheet.ModelCalls[0].Detail));
    }

    [Fact]
    public async Task A_Jev_pick_row_reads_as_a_sentence()
    {
        var sheet = await SheetWith(2, calls:
        [
            Call("c1", "2026-01-01T00:02:50Z", "labeling", 0, 0, ["jev", "jev-1.13.0"], [Try("jev", "jev-1.13.0", "answered")], items: 100, how: new JsonObject { ["jev"] = 94, ["firstCandidate"] = 5, ["fileName"] = 1 }),
            Call("c2", "2026-01-01T00:03:00Z", "labeling", 0, 0, answered: null, [Try("jev", "jev-1.13.0", "not-asked", "no-key", 0)]),
        ]);

        Assert.Equal(["Jev: picked 94 of 100, 5 took the model's first, 1 took the file name", "Jev: not asked (no key)"], sheet.ModelCalls.Select(static r => r.Who), StringComparer.Ordinal);
        Assert.Equal("00:02:50, Jev: picked 94 of 100, 5 took the model's first, 1 took the file name", sheet.ModelCalls[0].Name);
    }

    [Fact]
    public async Task A_product_surface_pick_row_reads_as_a_sentence()
    {
        var sheet = await SheetWith(2, calls:
        [
            Call("c1", "2026-01-01T00:02:50Z", "productSurfaces", 0, 0, ["jev", "jev-1.13.0"], [Try("jev", "jev-1.13.0", "answered")], items: 80,
                how: new JsonObject { ["merged"] = 12, ["kept"] = 58, ["rejected"] = 6, ["undecided"] = 4 }),
            Call("c2", "2026-01-01T00:03:00Z", "productSurfaces", 0, 0, answered: null, [Try("jev", "jev-1.13.0", "not-asked", "off", 0)]),
        ]);

        Assert.Equal(["Jev product surfaces: 80 groups, 12 merged, 6 rejected, 58 kept (4 undecided)", "Jev product surfaces: not asked (off)"], sheet.ModelCalls.Select(static r => r.Who), StringComparer.Ordinal);
    }

    [Fact]
    public async Task More_calls_than_the_sheet_holds_say_how_many_are_shown()
    {
        var calls = Enumerable.Range(1, 2).Select(n => Call(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"c{n}"), "2026-01-01T00:02:00Z", "labeling", 1, 1, ["codex", "gpt-6-luna"], [Try("codex", "gpt-6-luna", "answered")])).ToArray();

        var more = await SheetWith(205, calls: calls);
        var all = await SheetWith(2, calls: calls);

        Assert.Equal(("showing the last 2 of 205", true), (more.ModelMoreText, more.HasModelMore));
        Assert.False(all.HasModelMore);
    }

    [Fact]
    public async Task A_job_that_made_no_call_has_no_Model_calls_list()
    {
        var sheet = await SheetWith(0);

        Assert.False(sheet.HasModelCalls);
        Assert.Equal("Model calls", sheet.ModelCallsHeading);
    }

    [Fact]
    public async Task A_paused_jobs_sheet_shows_why_and_Resume_and_Resume_reads_the_job_again()
    {
        var line = "Paused: claude-code at its usage limit. Resume when one is back.";
        var sheet = await SheetWith(0, "waiting", "model-limit", ["building", line]);
        var reads = _f.Server.JobCalls;

        Assert.Equal((true, line, "Resume"), (sheet.IsPaused, sheet.PausedText, sheet.ResumeText));
        await sheet.Resume(Token);

        Assert.Equal([Id], _f.Server.ResumedJobs);
        Assert.True(_f.Server.JobCalls > reads);
    }

    [Fact]
    public async Task A_job_that_is_not_paused_has_no_Resume()
    {
        var sheet = await SheetWith(0);

        Assert.False(sheet.IsPaused);
    }
}
