using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>
/// The scoping line keeps a round log and treats a repeated finding as convergence (#1119, #1120): every Step that finds something appends to 'fact:roundLog'
/// without touching earlier entries and the drafter reads it, and evidence returning the same findings twice reaches the owner's stop instead of parking no-progress.
/// </summary>
public sealed class ScopingRoundLogTests
{
    private const string EvidenceEval = "sindri/evals/workflow.scoping/evidence-incomplete.jsonl";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Round_two_of_check_keeps_round_ones_findings_in_the_round_log()
    {
        var area = new InMemoryRunArea();
        var first = await Check(area, ScopingFacts.Expiry().Edit("acceptance", n => n["items"] = new JsonArray()).Without("rounds"));
        var firstLog = Log(first);
        var facts = ScopingFacts.Expiry().Edit("acceptance", n => n["items"]![0]!["form"] = "bogus").Without("rounds");   // why: the fixture acceptance holds items
        facts["roundLog"] = firstLog.ToJsonString();
        var second = await Check(area, facts);

        var log = Log(second);

        Assert.True(log.Count > firstLog.Count, "round 2 appends to the log");
        Assert.All(firstLog.Select((entry, i) => (entry, now: log[i])), pair => Assert.True(JsonNode.DeepEquals(pair.entry, pair.now), "an earlier entry is never rewritten"));
        int[] rounds = [1, 2];
        Assert.Equal(rounds, log.Select(static e => e!["round"]!.GetValue<int>()).Distinct().Order());   // why: every log entry is an object with a round
        Assert.All(log, static e => Assert.Equal("finding", e!["kind"]!.GetValue<string>()));   // why: every log entry is an object with a kind
    }

    [Fact, Trait("Lane", "unit")]
    public void The_drafter_reads_the_round_log() =>
        Assert.Contains(FactKey.From("fact:roundLog"), ScopingSteps.Draft.Inputs);

    [Fact, Trait("Lane", "unit")]
    public void The_same_evidence_findings_two_rounds_running_reach_the_stop_instead_of_parking()
    {
        var lines = ShippedRecipes.Read(EvidenceEval).Split('\n', StringSplitOptions.RemoveEmptyEntries);   // start .. first incomplete (draft) .. drafted (check)
        var first = Array.FindIndex(lines, static l => l.Contains("\"outcome\":\"incomplete\"", StringComparison.Ordinal));
        var pass = Array.FindIndex(lines, static l => l.Contains("\"outcome\":\"pass\"", StringComparison.Ordinal));
        var clear = Array.FindIndex(lines, static l => l.Contains("\"outcome\":\"clear\"", StringComparison.Ordinal));
        var again = JsonNode.Parse(lines[pass])!;   // why: the eval line is JSON; the gap search is latched shut after its clear round, so the check goes straight on to the questions
        again["expect"] = JsonNode.Parse("""{"state":"questions"}""");
        string[] repeat = [again.ToJsonString(), .. lines[(clear + 1)..(first + 1)]];   // check pass .. propose .. evidence incomplete, the same findings again, reported as converged
        var last = JsonNode.Parse(repeat[^1])!;   // why: the eval line is JSON
        last["trigger"]!["outcome"] = "converged";   // why: a trigger line has a trigger object; the evidence Step reports the repeat as its own outcome
        var carried = JsonNode.Parse("""{"fact:scopeMap":{},"fact:acceptance":{"items":[]},"fact:coverage":{"lenses":[]},"fact:openQuestions":[],"fact:scopePacket":{"hash":"sha256:0"},"fact:runs":[]}""")!.AsObject();   // why: the literal is a JSON object
        foreach (var (name, value) in carried.ToList())   // a real converged carries the packet the stop reads, beside the findings
        {
            _ = carried.Remove(name);
            last["trigger"]!["outputs"]![name] = value;   // why: the replayed incomplete line has a trigger with outputs
        }

        last["expect"] = JsonNode.Parse("""{"state":"stop"}""");
        repeat[^1] = last.ToJsonString();
        var read = EvalCaseReader.Read("converged", string.Join('\n', [.. lines, .. repeat]), EvalComposition.Options);
        var evalCase = Assert.IsType<EvalCase>(read.Case);
        var recipe = EvalComposition.Recipe("sindri/workflow.scoping.yaml");

        var verdict = EvalRunner.Run(evalCase, recipe, EvalComposition.Catalog, InMemoryChildRecipes.None);

        Assert.Null(verdict.Detail);
    }

    private static async Task<BlockStepResult> Check(InMemoryRunArea area, ScopingFacts facts) =>
        await new ScopeCheckStep(area).Run(ScopingStepContexts.Of("step.scope.check", facts), Token);

    /// <summary>The 'fact:roundLog' a result wrote, as its JSON array; the assertion fails when the Step wrote none.</summary>
    private static JsonArray Log(BlockStepResult result)
    {
        Assert.True(result.Result!.Outputs.TryGetValue(FactKey.From("fact:roundLog"), out var written), "the Step writes fact:roundLog");   // why: a completed result has outputs
        var text = written.ValueKind == JsonValueKind.String ? written.GetString()! : written.GetRawText();   // why: a string element has text
        return JsonNode.Parse(text)!.AsArray();   // why: the log is JSON
    }
}
