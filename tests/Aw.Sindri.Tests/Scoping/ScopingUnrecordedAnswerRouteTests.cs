using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>
/// An answer no acceptance item, open question or map change cites (SCP-ACC-003, #1205) is not something a redraft can fix: evidence reports 'open' for it, which goes to the owner's stop with the
/// finding open, the way a repeated list does. Any other failing rule still goes back to the drafter, and an uncovered file (SCP-COV-003) still goes to the exclusions step first.
/// </summary>
public sealed class ScopingUnrecordedAnswerRouteTests
{
    private const string EvidenceEval = "sindri/evals/workflow.scoping/evidence-incomplete.jsonl";

    // why: the gold answer log's first entry A1 is cited by acceptance items and surfaces; with those citations gone and no recordedAs, nothing records it
    private static ScopingFacts Unrecorded() => ScopingFacts.Expiry()
        .Edit("answerLog", static n => n[0]!["recordedAs"] = new JsonArray())
        .Edit("acceptance", static n => Uncite(n["items"]))
        .Edit("scopeMap", static n => Uncite(n["surfaces"]))
        .Edit("openQuestions", static n => Uncite(n));

    /// <summary>Takes A1 out of the citations of every object in the list: the places ACC-003 looks for a citation.</summary>
    private static void Uncite(JsonNode? list)
    {
        foreach (var citations in (list as JsonArray ?? []).Select(static x => x?["citations"] as JsonArray).OfType<JsonArray>())
        {
            foreach (var cited in citations.Where(static c => string.Equals(c?.GetValue<string>(), "A1", StringComparison.Ordinal)).ToList())
            {
                _ = citations.Remove(cited);
            }
        }
    }

    private static ScopingDeskResponse Evidence(ScopingFacts facts)
    {
        var ran = DeskBlocks.Run("step.scope.evidence", new ScopeFacts(facts));
        Assert.False(ran.IsError, ran.IsError ? ran.Error.Message : null);
        return ran.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public void Evidence_with_only_SCP_ACC_003_failing_reports_open_with_the_packet_and_the_findings()
    {
        var response = Evidence(Unrecorded());

        Assert.Equal("open", response.Outcome);
        Assert.Equal(["SCP-ACC-003"], response.Findings.Select(static f => f.Rule).Distinct(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Contains("findings", response.Outputs.Keys, StringComparer.Ordinal);
        Assert.Contains("roundLog", response.Outputs.Keys, StringComparer.Ordinal);
        Assert.Contains("scopeMap", response.Outputs.Keys, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evidence_with_SCP_ACC_003_and_another_rule_failing_stays_incomplete()
    {
        var facts = Unrecorded().Edit("acceptance", n => n["items"]![1]!["id"] = "AC-1");   // why: the gold acceptance list has two or more items

        Assert.Equal("incomplete", Evidence(facts).Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_open_scope_goes_to_the_stop()
    {
        var lines = ShippedRecipes.Read(EvidenceEval).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var first = Array.FindIndex(lines, static l => l.Contains("\"outcome\":\"incomplete\"", StringComparison.Ordinal));
        var open = JsonNode.Parse(lines[first])!;   // why: the eval line is JSON
        open["trigger"]!["outcome"] = "open";   // why: a trigger line has a trigger object
        var carried = JsonNode.Parse("""{"fact:scopeMap":{},"fact:acceptance":{"items":[]},"fact:coverage":{"lenses":[]},"fact:openQuestions":[],"fact:scopePacket":{"hash":"sha256:0"},"fact:runs":[]}""")!.AsObject();   // why: the literal is a JSON object
        foreach (var (name, value) in carried.ToList())   // a real open carries the packet the stop reads, beside the findings
        {
            _ = carried.Remove(name);
            open["trigger"]!["outputs"]![name] = value;   // why: the replayed incomplete line has a trigger with outputs
        }

        open["expect"] = JsonNode.Parse("""{"state":"stop"}""");
        var read = EvalCaseReader.Read("open", string.Join('\n', [.. lines[..first], open.ToJsonString()]), EvalComposition.Options);
        var evalCase = Assert.IsType<EvalCase>(read.Case);
        var recipe = EvalComposition.Recipe("sindri/workflow.scoping.yaml");

        var verdict = EvalRunner.Run(evalCase, recipe, EvalComposition.Catalog, InMemoryChildRecipes.None);

        Assert.Null(verdict.Detail);
    }
}
