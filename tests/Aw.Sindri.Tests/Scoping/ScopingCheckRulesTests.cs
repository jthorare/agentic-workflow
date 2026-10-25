using System.Text.Json.Nodes;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>
/// The rest of the spec's proof, "one broken fixture per failing rule exits 1 naming it": each rule here has one edit of the gold facts that
/// breaks it, checked at the evidence stage, where the outcome is incomplete.
/// </summary>
public sealed class ScopingCheckRulesTests
{
    private static async Task<ScopingCheckResponse> Evidence(ScopingFacts facts) =>
        (await new ScopingCheckService().Handle(new ScopingCheckRequest(facts, ScopeStage.Evidence), CancellationToken.None)).Value;

    private static void Fails(ScopingCheckResponse response, string rule, string item, ScopeOutcome outcome = ScopeOutcome.Incomplete)
    {
        Assert.Equal(outcome, response.Outcome);
        Assert.Contains(response.Findings, f => f.Rule == rule && f.Item == item);
        Assert.Equal(ScopeRuleStatus.Fail, response.Rules.Single(r => r.Rule == rule).Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SCP_COV_004_runs_on_the_gold_and_passes()
    {
        var response = await Evidence(ScopingFacts.Expiry());

        Assert.Equal(ScopeRuleStatus.Pass, response.Rules.Single(r => r.Rule == "SCP-COV-004").Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_depth_1_lens_node_that_is_neither_mapped_nor_excluded_is_SCP_COV_003()
    {
        var facts = ScopingFacts.Expiry().Edit("lenses", n => n["code"]!["nodes"]!.AsArray().Add(new JsonObject
        {
            ["key"] = "surface:route/links-export",
            ["kind"] = "surface",
            ["label"] = "Export a link",
            ["band"] = "product",
            ["lensItem"] = "L-code-99",
        }));

        Fails(await Evidence(facts), "SCP-COV-003", "surface:route/links-export");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_changed_file_that_is_neither_mapped_nor_unmapped_is_SCP_COV_004()
    {
        var facts = ScopingFacts.Expiry().Edit("anchors", n => n.AsArray().Add("code:src/sharing/audit.ts"));

        Fails(await Evidence(facts), "SCP-COV-004", "code:src/sharing/audit.ts");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lens_whose_foundCount_differs_from_its_coverage_row_is_SCP_LENS_001()
    {
        var facts = ScopingFacts.Expiry().Edit("lenses", n => n["code"]!["foundCount"] = 99);

        Fails(await Evidence(facts), "SCP-LENS-001", "code");   // a lens fault is the lens's: gathering again reproduces it, so it is never a regather
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_edge_that_ends_outside_the_found_nodes_is_SCP_LENS_001()
    {
        var facts = ScopingFacts.Expiry().Edit("lenses", n => n["code"]!["edges"]!.AsArray().Add(new JsonObject
        {
            ["from"] = "surface:route/links-create",
            ["to"] = "surface:route/nowhere",
            ["type"] = "part-of",
            ["provenance"] = "deterministic",
            ["confirmed"] = true,
        }));

        Fails(await Evidence(facts), "SCP-LENS-001", "code");   // a lens fault is the lens's: gathering again reproduces it, so it is never a regather
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_answer_recorded_nowhere_is_SCP_ACC_003()
    {
        var facts = ScopingFacts.Expiry().Edit("answerLog", n => n[0]!["recordedAs"] = new JsonArray());

        Fails(await Evidence(facts), "SCP-ACC-003", "A1");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_answer_recorded_as_an_id_that_does_not_exist_is_SCP_ACC_003()
    {
        var facts = ScopingFacts.Expiry().Edit("answerLog", n => n[0]!["recordedAs"] = new JsonArray("AC-99"));

        Fails(await Evidence(facts), "SCP-ACC-003", "A1");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_duplicate_acceptance_id_is_SCP_ACC_004()
    {
        var facts = ScopingFacts.Expiry().Edit("acceptance", n => n["items"]![1]!["id"] = "AC-1");

        Fails(await Evidence(facts), "SCP-ACC-004", "AC-1");
    }
}
