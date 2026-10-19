using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.LensLoops;

namespace Aw.Sindri.Tests.Workflow.Lens;

/// <summary>'step.lens.log' on the block runtime (line spec §4.1): appends the report, then next while a lens is queued, and at the round's end clean (0 CRITICAL, 0 HIGH) or findings.</summary>
[Lane(TestLane.Unit)]
public sealed class LensLogStepTests
{
    private const string Step = "step.lens.log";

    private static async Task<BlockStepResult> Run(string report, string rounds, string queue) =>
        await new LensLogStep().Run(LensStepContexts.Of(Step, ("lensReport", report), ("lensRounds", rounds), ("lensQueue", queue)), TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_queued_lens_left_reports_next_with_the_lens_taken_off_the_queue()
    {
        var result = await Run("""{"lens":"product","findings":[]}""", "[]", """["product","arch","security"]""");

        Assert.Equal("next", result.Result!.Outcome.Value);
        Assert.Equal(["lensQueue", "lensRounds"], LensStepContexts.Keys(result));
        Assert.Equal(["arch", "security"], LensStepContexts.Out(result, "lensQueue").AsArray().Select(static n => n!.GetValue<string>()), StringComparer.Ordinal);
        var entry = Assert.Single(LensStepContexts.Out(result, "lensRounds").AsArray())!;
        Assert.Equal("product", entry["lens"]!.GetValue<string>());
        Assert.Equal(1, entry["round"]!.GetValue<int>());
        Assert.Null(entry["closes"]);
    }

    [Fact]
    public async Task The_last_lens_with_nothing_blocking_reports_clean_and_closes_the_round()
    {
        var result = await Run("""{"lens":"security","findings":[{"severity":"MEDIUM","text":"a"},{"severity":"LOW","text":"b"}]}""", "[]", """["security"]""");

        Assert.Equal("clean", result.Result!.Outcome.Value);
        Assert.Equal(["lensRounds"], LensStepContexts.Keys(result));
        Assert.True(LensStepContexts.Out(result, "lensRounds").AsArray()[0]!["closes"]!.GetValue<bool>());
    }

    [Fact]
    public async Task The_last_lens_with_a_critical_or_high_finding_reports_findings_with_only_those()
    {
        var result = await Run("""{"lens":"security","findings":[{"severity":"HIGH","text":"h"},{"severity":"MEDIUM","text":"m"}]}""", "[]", """["security"]""");

        Assert.Equal("findings", result.Result!.Outcome.Value);
        var findings = LensStepContexts.Out(result, "findings").AsArray();
        var only = Assert.Single(findings)!;
        Assert.Equal("h", only["text"]!.GetValue<string>());
        Assert.Equal("security", only["lens"]!.GetValue<string>());
    }

    [Fact]
    public async Task The_round_end_counts_the_findings_of_every_lens_of_the_round()
    {
        const string earlier = """[{"round":1,"lens":"product","findings":[{"severity":"CRITICAL","text":"c"}]}]""";

        var result = await Run("""{"lens":"arch","findings":[]}""", earlier, """["arch"]""");

        Assert.Equal("findings", result.Result!.Outcome.Value);
        Assert.Equal(2, LensStepContexts.Out(result, "lensRounds").AsArray().Count);
        Assert.Equal("c", Assert.Single(LensStepContexts.Out(result, "findings").AsArray())!["text"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_new_round_starts_after_a_closed_one_and_ignores_its_findings()
    {
        const string closed = """[{"round":1,"lens":"product","findings":[{"severity":"HIGH","text":"old"}],"closes":true}]""";

        var result = await Run("""{"lens":"product","findings":[]}""", closed, """["product"]""");

        Assert.Equal("clean", result.Result!.Outcome.Value);
        var entries = LensStepContexts.Out(result, "lensRounds").AsArray();
        Assert.Equal(2, entries.Count);
        Assert.Equal(2, entries[1]!["round"]!.GetValue<int>());
    }

    [Fact]
    public async Task A_lens_that_is_not_applicable_needs_no_findings()
    {
        var result = await Run("""{"lens":"design","notApplicable":"no surface"}""", "[]", """["design"]""");

        Assert.Equal("clean", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task A_report_for_a_lens_that_is_not_queued_fails_the_Step()
    {
        var result = await Run("""{"lens":"ghost","findings":[]}""", "[]", """["product"]""");

        Assert.Null(result.Result);
        Assert.Contains("ghost", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_severity_off_the_scale_fails_the_Step()
    {
        var result = await Run("""{"lens":"product","findings":[{"severity":"blocking","text":"x"}]}""", "[]", """["product"]""");

        Assert.Null(result.Result);
        Assert.Contains("scale", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_report_with_neither_findings_nor_a_reason_fails_the_Step()
    {
        var result = await Run("""{"lens":"product"}""", "[]", """["product"]""");

        Assert.Null(result.Result);
    }

    [Fact]
    public async Task Facts_that_arrive_as_values_not_text_are_read_too()
    {
        var facts = new Dictionary<FactKey, JsonElement>
        {
            [FactKey.From("fact:lensReport")] = JsonDocument.Parse("""{"lens":"a","findings":[]}""").RootElement.Clone(),
            [FactKey.From("fact:lensRounds")] = JsonDocument.Parse("[]").RootElement.Clone(),
            [FactKey.From("fact:lensQueue")] = JsonDocument.Parse("""["a"]""").RootElement.Clone(),
        };

        var result = await new LensLogStep().Run(new StepContext(BlockId.From(Step), new Dictionary<string, JsonElement>(StringComparer.Ordinal), facts, InjectionSections.Empty), TestContext.Current.CancellationToken);

        Assert.Equal("clean", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task A_mid_round_report_that_carries_closes_still_gives_next_in_the_same_round()
    {
        var result = await Run("""{"lens":"product","closes":true,"round":9,"findings":[]}""", "[]", """["product","arch"]""");

        Assert.Equal("next", result.Result!.Outcome.Value);
        var entry = Assert.Single(LensStepContexts.Out(result, "lensRounds").AsArray())!;
        Assert.Equal(1, entry["round"]!.GetValue<int>());
        Assert.Null(entry["closes"]);
    }

    [Fact]
    public async Task A_copied_closes_does_not_hide_a_high_finding_in_a_clean_round()
    {
        const string earlier = """[{"round":1,"lens":"product","findings":[{"severity":"HIGH","text":"h"}]}]""";

        var result = await Run("""{"lens":"arch","closes":true,"findings":[]}""", earlier, """["arch"]""");

        Assert.Equal("findings", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task A_mixed_case_severity_counts_on_the_scale()
    {
        var result = await Run("""{"lens":"arch","findings":[{"severity":"High","text":"h"}]}""", "[]", """["arch"]""");

        Assert.Equal("findings", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task A_lens_already_logged_in_the_open_round_starts_a_new_round()
    {
        const string open = """[{"round":1,"lens":"product","findings":[{"severity":"HIGH","text":"old"}]}]""";

        var result = await Run("""{"lens":"product","findings":[]}""", open, """["product"]""");

        Assert.Equal("clean", result.Result!.Outcome.Value);
        Assert.Equal(2, LensStepContexts.Out(result, "lensRounds").AsArray()[1]!["round"]!.GetValue<int>());
    }

    [Fact]
    public async Task The_gold_lens_rounds_replay_as_two_rounds_of_five_lenses()
    {
        var gold = (await File.ReadAllLinesAsync(Path.Combine(RepoRoot(), "tests", "fixtures", "line", "gold", "design", "expiry", "lens-rounds.jsonl"), TestContext.Current.CancellationToken)).Select(static l => JsonNode.Parse(l)!.AsObject()).ToList();
        var lenses = gold.Take(5).Select(static e => e["lens"]!.GetValue<string>()).ToList();
        var rounds = "[]";
        var outcomes = new List<(int Round, string Outcome)>();
        foreach (var report in gold)
        {
            var round = report["round"]!.GetValue<int>();
            var left = lenses.SkipWhile(l => l != report["lens"]!.GetValue<string>()).ToList();
            var asReport = (JsonObject)report.DeepClone();
            _ = asReport.Remove("round");
            var result = await Run(asReport.ToJsonString(), rounds, JsonSerializer.Serialize(left));
            outcomes.Add((round, result.Result!.Outcome.Value));
            rounds = LensStepContexts.Out(result, "lensRounds").ToJsonString();
        }

        Assert.Equal(
            [(1, "next"), (1, "next"), (1, "next"), (1, "next"), (1, "findings"), (2, "next"), (2, "next"), (2, "next"), (2, "next"), (2, "clean")],
            outcomes);
        Assert.Equal(gold.Select(static e => e["round"]!.GetValue<int>()), JsonNode.Parse(rounds)!.AsArray().Select(static e => e!["round"]!.GetValue<int>()));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgenticWorkflow.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("the repo root was not found");
    }
}
