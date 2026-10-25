using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>#993: a fault in a lens (edges that end outside its found nodes, a row that disagrees) is the lens's, not the scope's: gather fails on it and the check names it, instead of looping through regather.</summary>
[Lane(TestLane.Unit)]
public sealed class LensFaultTests
{
    private static JsonObject Dangling(string from, string to) => new() { ["from"] = from, ["to"] = to, ["type"] = "part-of", ["provenance"] = "deterministic", ["confirmed"] = true };

    private static (JsonObject Entry, JsonNode Row) Gold(Action<JsonObject>? edit = null)
    {
        var facts = ScopingFacts.Expiry();
        var entry = (JsonObject)JsonNode.Parse(JsonNode.Parse(facts["lenses"])!["code"]!.ToJsonString())!;
        edit?.Invoke(entry);
        var row = JsonNode.Parse(facts["coverage"])!["lenses"]!.AsArray().Single(static r => r!["lens"]!.GetValue<string>() == "code")!;
        return (entry, JsonNode.Parse(row.ToJsonString())!);
    }

    [Fact]
    public void The_gold_lens_has_no_fault()
    {
        var (entry, row) = Gold();

        Assert.Empty(LensShape.Faults("code", entry, row));
    }

    [Fact]
    public void An_edge_ending_outside_the_found_nodes_is_one_counted_fault()
    {
        var (entry, row) = Gold(e => e["edges"]!.AsArray().Add(Dangling("surface:route/links-create", "surface:route/nowhere")));

        var fault = Assert.Single(LensShape.Faults("code", entry, row));

        Assert.Contains("served 1 edges that end outside its found nodes and anchors", fault.Message, StringComparison.Ordinal);
        Assert.Contains("surface:route/nowhere", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_edge_that_ends_at_an_anchor_is_not_a_fault()
    {
        var (entry, row) = Gold(e => e["edges"]!.AsArray().Add(Dangling(e["anchors"]![0]!.GetValue<string>(), e["nodes"]![0]!["key"]!.GetValue<string>())));

        Assert.Empty(LensShape.Faults("code", entry, row));
    }

    [Fact]
    public void A_depth_that_differs_from_the_coverage_row_is_a_fault()
    {
        var (entry, row) = Gold(e => e["depth"] = 9);

        var fault = Assert.Single(LensShape.Faults("code", entry, row));

        Assert.Contains("depth differs", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Gather_names_SCP_GATHER_001_with_a_count_and_the_lens_not_the_scope()
    {
        var facts = ScopingFacts.Expiry().Edit("lenses", n => n["code"]!["edges"]!.AsArray().Add(Dangling("surface:route/links-create", "surface:route/nowhere")));

        var message = LensShape.GatherFault(JsonNode.Parse(facts["lenses"])!, JsonNode.Parse(facts["coverage"])!);

        Assert.StartsWith("SCP-GATHER-001: the code lens served 1 edges", message, StringComparison.Ordinal);
        Assert.Contains("a fault in the lens, not in the scope", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_coverage_row_recording_edges_out_of_scope_with_only_in_scope_edges_left_is_clean()
    {
        var facts = ScopingFacts.Expiry().Edit("coverage", n =>
        {
            var row = n["lenses"]!.AsArray().Single(static r => r!["lens"]!.GetValue<string>() == "code")!;
            row["searched"]!["sources"]!.AsArray().Add(new JsonObject { ["id"] = "edges", ["state"] = "unavailable", ["reason"] = "edges-out-of-scope" });
            row["limits"]!.AsArray().Add(new JsonObject { ["why"] = "edges-out-of-scope", ["not"] = "5 edges outside the lens" });
        });

        Assert.Null(LensShape.GatherFault(JsonNode.Parse(facts["lenses"])!, JsonNode.Parse(facts["coverage"])!));
    }

    [Fact]
    public void Gather_on_clean_lenses_has_no_fault()
    {
        var facts = ScopingFacts.Expiry();

        Assert.Null(LensShape.GatherFault(JsonNode.Parse(facts["lenses"])!, JsonNode.Parse(facts["coverage"])!));
    }

    [Fact]
    public async Task Ten_thousand_dangling_edges_are_one_finding_with_at_most_three_examples()
    {
        var facts = ScopingFacts.Expiry().Edit("lenses", n =>
        {
            for (var i = 0; i < 10_000; i++)
            {
                n["code"]!["edges"]!.AsArray().Add(Dangling("surface:route/links-create", string.Create(CultureInfo.InvariantCulture, $"code:gone{i}")));
            }
        });

        var response = (await new ScopingCheckService().Handle(new ScopingCheckRequest(facts, ScopeStage.Evidence), CancellationToken.None)).Value;

        var finding = Assert.Single(response.Findings, static f => f.Rule == "SCP-LENS-001");
        Assert.Contains("served 10000 edges", finding.Message, StringComparison.Ordinal);
        Assert.Equal(LensShape.Examples, finding.Message.Split("code:gone").Length - 1);
    }

    [Fact]
    public async Task The_evidence_Step_fails_on_a_lens_fault_instead_of_asking_for_a_regather()
    {
        var facts = ScopingFacts.Expiry().Without("rounds").Without("scopePacket")
            .Edit("lenses", n => n["code"]!["edges"]!.AsArray().Add(Dangling("surface:route/links-create", "surface:route/nowhere")));
        var step = new ScopeEvidenceStep(new InMemoryRunArea(), new InMemoryPackets(), new InMemoryRepoCheckouts(), new FakeRunDiff());

        var result = await step.Run(ScopingStepContexts.Of("step.scope.evidence", facts), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("SCP-LENS-001 fails at step.scope.evidence", result.Failure, StringComparison.Ordinal);
    }
}
