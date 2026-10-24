using System.Globalization;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The context engine no longer cuts a band at 100, so a lens keeps every node and item it finds, is never truncated, and SCP-LENS-001 does not cap the ids a lens file holds.</summary>
public sealed class LensBandUncappedTests
{
    private static LensBuilder Filled(int nodes, int items)
    {
        var lens = new LensBuilder("code", ["feature:links"]);
        for (var i = 0; i < nodes; i++)
        {
            lens.Node(string.Create(CultureInfo.InvariantCulture, $"code:src/file{i}.ts"), "code", label: null, band: "code");
        }

        for (var i = 0; i < items; i++)
        {
            lens.Item("record", label: null, citations: []);
        }

        return lens;
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lens_keeps_every_node_and_item_past_100_and_reports_no_truncation()
    {
        var entry = Filled(130, 40).Entry("v1", 1, ["state"]);

        Assert.Equal(130, entry["nodes"]!.AsArray().Count);
        Assert.Equal(40, entry["items"]!.AsArray().Count);
        Assert.Equal(170, entry["foundCount"]!.GetValue<int>());
        Assert.Empty(entry["truncated"]!.AsArray());
    }

    [Fact, Trait("Lane", "unit")]
    public void The_coverage_row_lists_every_found_id_past_100()
    {
        var row = Filled(130, 40).Row("v1", 1, ["state"]);

        Assert.Equal(170, row["found"]!["count"]!.GetValue<int>());
        Assert.Equal(170, row["found"]!["ids"]!.AsArray().Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lens_file_holding_more_than_100_ids_that_match_its_row_passes_SCP_LENS_001()
    {
        var lens = Filled(130, 40);
        var facts = ScopingFacts.Expiry()
            .Edit("lenses", n => n["code"] = lens.Entry("v1", 1, ["state"]))
            .Edit("coverage", n => n["lenses"]![n["lenses"]!.AsArray().ToList().FindIndex(static r => r!["lens"]!.GetValue<string>() == "code")] = lens.Row("v1", 1, ["state"]));

        var response = (await new ScopingCheckService().Handle(new ScopingCheckRequest(facts, ScopeStage.Evidence), CancellationToken.None)).Value;

        Assert.DoesNotContain(response.Findings, static f => f.Rule == "SCP-LENS-001");
    }
}
