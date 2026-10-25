using System.Text.Json.Nodes;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>A lens serves only edges whose two ends are its found nodes or anchors, and records how many it left out as the edges-out-of-scope limit (#993).</summary>
public sealed class LensEdgesInScopeTests
{
    private static LensGatherResult Assembled()
    {
        var lenses = JsonNode.Parse("""{ "anchors": [ { "key": "feature:a" } ] }""");
        var code = JsonNode.Parse("""{ "files": [ { "path": "src/a.ts" } ], "edges": [ { "from": "code:src/a.ts", "to": "feature:a", "type": "implements" }, { "from": "code:src/a.ts", "to": "feature:root.package-24", "type": "implements" }, { "from": "code:src/other.test.ts", "to": "feature:root.package-24", "type": "tests" }, { "from": "code:src/other.test.ts", "to": "feature:a", "type": "tests" } ] }""");
        return LensGather.Assemble(lenses, code, history: null, ["feature:a"]);
    }

    private static JsonNode CodeRow(LensGatherResult result) =>
        result.Coverage["lenses"]!.AsArray().Single(static r => r!["lens"]!.GetValue<string>() == "code")!;

    [Fact, Trait("Lane", "unit")]
    public void The_code_lens_serves_no_edge_ending_outside_its_nodes_and_anchors_and_records_the_count()
    {
        var result = Assembled();

        var edges = result.Lenses["code"]!["edges"]!.AsArray();
        var edge = Assert.Single(edges);
        Assert.Equal("feature:a", edge!["to"]!.GetValue<string>());
        Assert.Contains(CodeRow(result)["limits"]!.AsArray(), static l => l!["why"]!.GetValue<string>() == "edges-out-of-scope" && l["not"]!.GetValue<string>() == "3 edges outside the lens");
        Assert.NotNull(ScopeFixLines.ByName("edges-out-of-scope"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lens_with_every_edge_in_scope_records_no_such_limit()
    {
        var lenses = JsonNode.Parse("""{ "anchors": [ { "key": "feature:a" } ] }""");
        var code = JsonNode.Parse("""{ "files": [ { "path": "src/a.ts" } ], "edges": [ { "from": "code:src/a.ts", "to": "feature:a", "type": "implements" } ] }""");

        var row = CodeRow(LensGather.Assemble(lenses, code, history: null, ["feature:a"]));

        Assert.DoesNotContain(row["limits"]!.AsArray(), static l => l!["why"]!.GetValue<string>() == "edges-out-of-scope");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_check_accepts_the_gathered_lens_and_its_edges_out_of_scope_limit()
    {
        var result = Assembled();
        var facts = ScopingFacts.Expiry()
            .Edit("lenses", n => n["code"] = result.Lenses["code"]!.DeepClone())
            .Edit("coverage", n => n["lenses"]![n["lenses"]!.AsArray().ToList().FindIndex(static r => r!["lens"]!.GetValue<string>() == "code")] = CodeRow(result).DeepClone());

        var response = (await new ScopingCheckService().Handle(new ScopingCheckRequest(facts, ScopeStage.Evidence), CancellationToken.None)).Value;

        Assert.DoesNotContain(response.Findings, static f => (string.Equals(f.Rule, "SCP-LENS-001", StringComparison.Ordinal) || string.Equals(f.Rule, "SCP-COV-002", StringComparison.Ordinal)) && f.Item == "code");
    }
}
