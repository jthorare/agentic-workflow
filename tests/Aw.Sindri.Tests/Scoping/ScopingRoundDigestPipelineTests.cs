using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>
/// #1397: the round digest (#1119) through the pipeline, not through RoundLog alone. The log only grows because the code Steps append to a clone of it and no agent Step declares
/// 'fact:roundLog' as an output (the engine rejects an undeclared one), and a finding of round 1 that round 2's exclusion covers is closed in the digest the next Step reads.
/// </summary>
public sealed class ScopingRoundDigestPipelineTests
{
    private const string RedraftEval = "sindri/evals/workflow.scoping/check-gaps-redraft.jsonl";

    private static async Task<ScopingDeskResponse> Check(ScopingFacts facts) =>
        (await new ScopingDeskService().Handle(new ScopingDeskRequest("check", facts), TestContext.Current.CancellationToken)).Value;

    public static TheoryData<string> AgentSteps => ["step.scope.draft", "step.scope.challenge", "step.scope.exclude", "step.interview.questions", "step.interview.apply"];

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(AgentSteps))]
    public void No_agent_step_declares_the_round_log_or_digest_as_an_output(string step)
    {
        var descriptor = EvalComposition.Catalog.Find(Core.Workflow.Ids.BlockId.From(step));

        Assert.NotNull(descriptor);
        Assert.DoesNotContain(descriptor.Outputs, static o => string.Equals(o.Fact.Value, "fact:roundLog", StringComparison.Ordinal) || string.Equals(o.Fact.Value, "fact:roundDigest", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_draft_that_reports_the_round_log_is_rejected_by_the_engine_and_the_run_stays_at_draft()
    {
        var lines = ShippedRecipes.Read(RedraftEval).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var draft = Array.FindIndex(lines, static l => l.Contains("\"outcome\":\"drafted\"", StringComparison.Ordinal));
        var smuggled = JsonNode.Parse(lines[draft])!;   // why: the eval line is JSON
        smuggled["trigger"]!["outputs"]!["fact:roundLog"] = JsonNode.Parse("[]");   // why: a drafted trigger has outputs
        smuggled["expect"] = JsonNode.Parse("""{"state":"check"}""");
        var read = EvalCaseReader.Read("smuggled", string.Join('\n', [.. lines[..draft], smuggled.ToJsonString()]), EvalComposition.Options);
        var evalCase = Assert.IsType<EvalCase>(read.Case);

        var verdict = EvalRunner.Run(evalCase, EvalComposition.Recipe("sindri/workflow.scoping.yaml"), EvalComposition.Catalog, InMemoryChildRecipes.None);

        Assert.False(verdict.Passed);
        Assert.Contains("step.scope.draft reported fact:roundLog, which it doesn't declare", verdict.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_round_1_finding_excluded_in_round_2_is_closed_in_the_digest_round_3_reads()
    {
        var round1 = await Check(ScopingFacts.Expiry().Edit("acceptance", static n => n["items"] = new JsonArray()).Without("rounds"));
        var log1 = JsonNode.Parse(round1.Outputs["roundLog"])!.AsArray();
        var key = log1[0]!["key"]!.GetValue<string>();
        var item = key[(key.IndexOf(':', StringComparison.Ordinal) + 1)..];
        Assert.Contains(key, JsonNode.Parse(round1.Outputs["roundDigest"])!["open"]!.AsArray().Select(static e => e!["key"]!.GetValue<string>()), StringComparer.Ordinal);   // why: round 1 leaves it open

        var facts2 = ScopingFacts.Expiry().Edit("acceptance", static n => n["items"] = new JsonArray()).Without("rounds")
            .Edit("scopeMap", n => n["excluded"] = new JsonArray(new JsonObject { ["item"] = item, ["reason"] = "generated" }));   // why: the fixture map is a JSON object
        facts2["roundLog"] = round1.Outputs["roundLog"];
        var round2 = await Check(facts2);

        var digest = JsonNode.Parse(round2.Outputs["roundDigest"])!;
        var closed = digest["closed"]!.AsArray().Single(e => string.Equals(e!["key"]!.GetValue<string>(), key, StringComparison.Ordinal))!;
        Assert.Equal("exclusion", closed["by"]!.GetValue<string>());
        Assert.DoesNotContain(key, digest["open"]!.AsArray().Select(static e => e!["key"]!.GetValue<string>()), StringComparer.Ordinal);
        Assert.True(JsonNode.Parse(round2.Outputs["roundLog"])!.AsArray().Count > log1.Count - 1);   // why: round 2 appended to the log
    }
}
