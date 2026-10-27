using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Scoping.Relevance;

/// <summary>'step.scope.propose' carries the relevance review's map fixes through and records what became of them (spec 5.3, open question 1: recorded, none submitted).</summary>
[Lane(TestLane.Unit)]
public sealed class ProposeMapFixesTests
{
    private static readonly RepoName _repo = RepoName.From("fixture-notes");

    private static string Fixes() => new JsonObject
    {
        ["fixes"] = new JsonArray(
            new JsonObject { ["id"] = "L-code-3", ["node"] = "code:src/admin/Shell.ts", ["action"] = "ignore", ["relabelTo"] = null, ["reason"] = "A shared shell.", ["citation"] = "L-code-3", ["status"] = "proposed" },
            new JsonObject { ["id"] = "L-product-2", ["node"] = "feature:billing-export", ["action"] = "relabel", ["relabelTo"] = "feature:billing", ["reason"] = "Billing.", ["citation"] = "R1", ["status"] = "proposed" }),
    }.ToJsonString();

    private static string Map() => ScopingFacts.Expiry().Edit("scopeMap", map =>
    {
        foreach (var surface in map["surfaces"]!.AsArray().OfType<JsonObject>().Where(static s => s["id"]!.GetValue<string>() == "S22"))
        {
            _ = surface.Remove("key");
            surface["graphStatus"] = "unknown";
        }
    })["scopeMap"];

    private static async Task<BlockStepResult> Run(ScopingFacts facts, bool withRepo)
    {
        var context = ScopingStepContexts.Of("step.scope.propose", facts) with { Address = "T-1/scoping/1", Repo = withRepo ? _repo : null };
        return await new ScopeProposeStep(new InMemoryScopeRunProposals()).Run(context, TestContext.Current.CancellationToken);
    }

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;

    [Fact]
    public async Task A_proposed_run_records_each_fix_and_changes_nothing_else_about_it()
    {
        var result = await Run(new ScopingFacts { ["scopeMap"] = Map(), ["mapFixes"] = Fixes() }, withRepo: true);

        Assert.Equal("proposed", result.Result!.Outcome.Value);
        var fixes = Out(result, "mapFixes")["fixes"]!.AsArray();
        Assert.Equal(2, fixes.Count);
        Assert.All(fixes, static f => Assert.Equal("recorded", f!["status"]!.GetValue<string>()));
        Assert.Equal(["L-code-3", "L-product-2"], fixes.Select(static f => f!["id"]!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal("feature:billing", fixes[1]!["relabelTo"]!.GetValue<string>());
        Assert.Equal("code:src/admin/Shell.ts", fixes[0]!["node"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_skipped_run_marks_the_fixes_skipped()
    {
        var result = await Run(new ScopingFacts { ["scopeMap"] = Map(), ["mapFixes"] = Fixes() }, withRepo: false);

        Assert.Equal("skipped", result.Result!.Outcome.Value);
        Assert.All(Out(result, "mapFixes")["fixes"]!.AsArray(), static f => Assert.Equal("skipped", f!["status"]!.GetValue<string>()));
    }

    [Fact]
    public async Task With_no_map_fixes_fact_it_reports_an_empty_list()
    {
        var result = await Run(new ScopingFacts { ["scopeMap"] = Map() }, withRepo: true);

        Assert.Empty(Out(result, "mapFixes")["fixes"]!.AsArray());
    }

    [Fact]
    public async Task The_scope_map_is_still_written_as_before()
    {
        var result = await Run(new ScopingFacts { ["scopeMap"] = Map(), ["mapFixes"] = Fixes() }, withRepo: true);

        Assert.Equal("new", Out(result, "scopeMap")["surfaces"]!.AsArray().Single(static s => s!["id"]!.GetValue<string>() == "S22")!["graphStatus"]!.GetValue<string>());
    }
}
