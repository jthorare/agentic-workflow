using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>Every edge a gathered lens serves ends at a node it serves or an anchor, also when the lens finds more than 100 nodes (#993).</summary>
public sealed class LensEdgesPastCapTests
{
    private const int Files = 120;

    private static LensGatherResult Assembled()
    {
        var lenses = JsonNode.Parse("""{ "anchors": [ { "key": "feature:a" } ] }""");
        var files = new JsonArray();
        var edges = new JsonArray();
        for (var i = 0; i < Files; i++)
        {
            var path = string.Create(CultureInfo.InvariantCulture, $"src/f{i}.ts");
            files.Add(new JsonObject { ["path"] = path });
            edges.Add(new JsonObject { ["from"] = "code:" + path, ["to"] = "feature:a", ["type"] = "implements" });
        }

        var code = new JsonObject { ["files"] = files, ["edges"] = edges };
        return LensGather.Assemble(lenses, code, history: null, ["feature:a"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_edge_of_a_lens_that_finds_more_than_100_nodes_ends_at_a_node_it_serves_or_an_anchor()
    {
        var entry = Assembled().Lenses["code"]!;
        var ends = entry["nodes"]!.AsArray().Select(static n => n!["key"]!.GetValue<string>())
            .Concat(entry["anchors"]!.AsArray().Select(static a => a!.GetValue<string>()))
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(entry["edges"]!.AsArray(), e =>
        {
            Assert.Contains(e!["from"]!.GetValue<string>(), ends);
            Assert.Contains(e["to"]!.GetValue<string>(), ends);
        });
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_check_finds_no_SCP_LENS_001_on_a_gathered_lens_of_more_than_100_nodes()
    {
        var result = Assembled();
        var row = result.Coverage["lenses"]!.AsArray().Single(static r => r!["lens"]!.GetValue<string>() == "code")!;
        var facts = ScopingFacts.Expiry()
            .Edit("lenses", n => n["code"] = result.Lenses["code"]!.DeepClone())
            .Edit("coverage", n => n["lenses"]![n["lenses"]!.AsArray().ToList().FindIndex(static r => r!["lens"]!.GetValue<string>() == "code")] = row.DeepClone());

        var response = (await new ScopingCheckService().Handle(new ScopingCheckRequest(facts, ScopeStage.Evidence), CancellationToken.None)).Value;

        Assert.DoesNotContain(response.Findings, static f => f.Rule == "SCP-LENS-001" && f.Item == "code");
    }
}
