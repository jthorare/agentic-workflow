using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>
/// A scope that comes back incomplete only because of SCP-COV-003 (#1114) goes to a narrow exclusions step, not to a full redraft: evidence reports 'uncovered'
/// for it, 'incomplete' still for anything else, and 'converged' still outranks it when the same list comes back twice.
/// </summary>
public sealed class ScopingExclusionsRouteTests
{
    private const string EvidenceEval = "sindri/evals/workflow.scoping/evidence-incomplete.jsonl";
    private const string Cov3 = "SCP-COV-003";
    private const string Key = "code:src/new/near.ts";

    private static ScopingFacts Uncovered() => ScopingFacts.Expiry().Edit("lenses", n =>
    {
        n["code"]!["nodes"]!.AsArray().Add(new JsonObject   // why: the gold lens has a code band with nodes and edges
        {
            ["key"] = Key,
            ["kind"] = "file",
            ["label"] = "src/new/near.ts",
            ["band"] = "code",
            ["lensItem"] = "L-code-12",   // why: the lens ids must stay the coverage row's found ids, so the new node reuses one the map does not cite
        });
        n["code"]!["edges"]!.AsArray().Add(new JsonObject   // why: see above
        {
            ["from"] = Key,
            ["to"] = "surface:route/links-create",
            ["type"] = "implements",
            ["provenance"] = "deterministic",
            ["confirmed"] = true,
        });
    });

    private static ScopingDeskResponse Evidence(ScopingFacts facts)
    {
        var ran = DeskBlocks.Run("step.scope.evidence", new ScopeFacts(facts));
        Assert.False(ran.IsError, ran.IsError ? ran.Error.Message : null);
        return ran.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public void Evidence_with_only_SCP_COV_003_failing_reports_uncovered_with_the_findings_and_the_round_log()
    {
        var response = Evidence(Uncovered());

        Assert.Equal("uncovered", response.Outcome);
        Assert.Equal([Cov3], response.Findings.Select(static f => f.Rule).Distinct(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Contains("findings", response.Outputs.Keys, StringComparer.Ordinal);
        Assert.Contains("roundLog", response.Outputs.Keys, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evidence_with_SCP_COV_003_and_another_rule_failing_stays_incomplete()
    {
        var facts = Uncovered().Edit("acceptance", n => n["items"]![1]!["id"] = "AC-1");   // why: the gold acceptance list has two or more items

        Assert.Equal("incomplete", Evidence(facts).Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_SCP_COV_003_list_twice_is_converged_not_uncovered()
    {
        var facts = Uncovered();
        facts["roundLog"] = new JsonArray(new JsonObject
        {
            ["round"] = 1,
            ["step"] = "evidence",
            ["kind"] = "finding",
            ["key"] = $"{Cov3}:{Key}",
            ["text"] = "earlier round",
            ["by"] = "step.scope.evidence",
        }).ToJsonString();

        Assert.Equal("converged", Evidence(facts).Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_uncovered_scope_goes_to_the_exclusions_step_and_back_to_the_check()
    {
        var lines = ShippedRecipes.Read(EvidenceEval).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var first = Array.FindIndex(lines, static l => l.Contains("\"outcome\":\"incomplete\"", StringComparison.Ordinal));
        var uncovered = JsonNode.Parse(lines[first])!;   // why: the eval line is JSON
        uncovered["trigger"]!["outcome"] = "uncovered";   // why: a trigger line has a trigger object
        uncovered["expect"] = JsonNode.Parse("""{"state":"exclude"}""");
        const string Excluded = """{"trigger":{"kind":"step.completed","outcome":"excluded","outputs":{"fact:scopeMap":{"surfaces":[],"implications":[],"workstreams":[],"flagged":[],"excluded":[],"unmapped":[],"unresolved":[]}}},"expect":{"state":"check"}}""";
        var read = EvalCaseReader.Read("exclusions", string.Join('\n', [.. lines[..first], uncovered.ToJsonString(), Excluded]), EvalComposition.Options);
        var evalCase = Assert.IsType<EvalCase>(read.Case);
        var recipe = EvalComposition.Recipe("sindri/workflow.scoping.yaml");

        var verdict = EvalRunner.Run(evalCase, recipe, EvalComposition.Catalog, InMemoryChildRecipes.None);

        Assert.Null(verdict.Detail);
    }
}
