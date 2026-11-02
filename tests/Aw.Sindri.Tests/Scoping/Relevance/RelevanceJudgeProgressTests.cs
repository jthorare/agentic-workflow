using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Scoping.Steps;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Tests.Scoping.Relevance.RelevanceFixtures;

namespace Aw.Sindri.Tests.Scoping.Relevance;

/// <summary>A long judge run says where it is and where its time went (#1440): 'k/N ruled' lines in the log, and the timings in the result.</summary>
[Lane(TestLane.Unit)]
public sealed class RelevanceJudgeProgressTests
{
    private const int EntryCount = 60;
    private static readonly TimeSpan _callTime = TimeSpan.FromMilliseconds(10);

    private readonly FakeTimeProvider _clock = new();
    private readonly ListLogger<RelevanceJudgeStep> _log = new();

    private static ScopingFacts Facts()
    {
        var nodes = Enumerable.Range(1, EntryCount)
            .Select(static i => Node("code:src/f" + i.ToString(CultureInfo.InvariantCulture) + ".ts", "code", "f.ts", "code", "L-code-" + i.ToString(CultureInfo.InvariantCulture)))
            .ToArray();
        var lenses = Lenses();
        lenses["code"] = Lens(["surface:screen/invite-dialog"], EntryCount, nodes, [], []);
        lenses["product"] = Lens(["surface:screen/invite-dialog"], 0, [], [], []);
        lenses["history"] = Lens(["surface:screen/invite-dialog"], 0, [], [], []);
        return new ScopingFacts { ["brief"] = Brief().ToJsonString(), ["lenses"] = lenses.ToJsonString() };
    }

    private async Task<BlockStepResult> Run(int parallel)
    {
        var arguments = new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal)
        {
            ["parallel"] = System.Text.Json.JsonSerializer.SerializeToElement(parallel),
        };
        var step = new RelevanceJudgeStep(new ClockedJudge(_clock, _callTime), new PassThroughScrubber(), _clock, _log);
        return await step.Run(ScopingStepContexts.Of("step.scope.relevance-judge", Facts()) with { Arguments = arguments }, TestContext.Current.CancellationToken);
    }

    private static JsonNode Timings(BlockStepResult result) =>
        JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:relevanceRulings")].GetRawText())!["timings"]!;

    [Fact]
    public async Task The_result_carries_the_timings_of_the_run()
    {
        var result = await Run(parallel: 1);

        var timings = Timings(result);
        Assert.Equal(EntryCount, timings["calls"]!.GetValue<long>());
        Assert.Equal(10, timings["firstCallMs"]!.GetValue<long>());
        Assert.Equal(10 * EntryCount, timings["totalMs"]!.GetValue<long>());
        Assert.Equal(10, timings["slowestCallMs"]!.GetValue<long>());
    }

    [Fact]
    public async Task The_total_is_at_least_the_first_call_when_calls_run_side_by_side()
    {
        var result = await Run(parallel: 4);

        var timings = Timings(result);
        Assert.Equal(EntryCount, timings["calls"]!.GetValue<long>());
        Assert.True(timings["totalMs"]!.GetValue<long>() >= timings["firstCallMs"]!.GetValue<long>());
        Assert.True(timings["slowestCallMs"]!.GetValue<long>() >= 10);
    }

    [Fact]
    public async Task The_log_names_the_run_then_every_25_rulings_then_the_end()
    {
        _ = await Run(parallel: 1);

        var lines = _log.Lines;
        Assert.Contains(lines, static l => l.Contains("relevance-judge: 60 entries, parallel 1", StringComparison.Ordinal));
        var progress = lines.Where(static l => l.Contains(" ruled", StringComparison.Ordinal)).ToArray();
        Assert.Equal(3, progress.Length);
        Assert.Contains("relevance-judge: 25/60 ruled", progress[0], StringComparison.Ordinal);
        Assert.Contains("relevance-judge: 50/60 ruled", progress[1], StringComparison.Ordinal);
        Assert.Contains("relevance-judge: 60/60 ruled", progress[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_end_line_holds_the_timings()
    {
        _ = await Run(parallel: 1);

        Assert.Contains(_log.Lines, static l => l.Contains("firstCallMs=10", StringComparison.Ordinal) && l.Contains("totalMs=600", StringComparison.Ordinal));
    }

    private sealed class ClockedJudge(FakeTimeProvider clock, TimeSpan callTime) : IJudgeChoice
    {
        public ValueTask<ChooseAnswer> Choose(ChooseRequest request, CancellationToken cancellationToken)
        {
            clock.Advance(callTime);
            return ValueTask.FromResult(new ChooseAnswer(ChooseStatus.Decided, "in", "model", 0.9, "jev", "JD-1"));
        }
    }
}
