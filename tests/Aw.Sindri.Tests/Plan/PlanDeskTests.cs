using System.Text.Json.Nodes;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Features.Plan;
using Aw.Sindri.Testing.Line;

namespace Aw.Sindri.Tests.Plan;

/// <summary>The plan block Steps run at the desk (line spec §6.1): 'check' reports pass or gaps, 'evidence' assembles the candidate packet or reports it incomplete.</summary>
public sealed class PlanDeskTests
{
    private static readonly LineDeskContext _context = new(
        new PacketIdentity("line-expiry-plan", "fixture-labs/fixture-notes", "3288642cd47359fff2642b8bf429976915ca4894"),
        ScopeHash: "sha256:48b9dc81f8935c81f465f785a6e7dba0abd9697480786ad7994d1475da00c973",
        DesignHash: "sha256:3deb049307bf38e0981ed4ed374f7d84a63f305f0067a247c10f839dfd4f87e4");

    private static async Task<(bool IsError, string Code, LineDeskResponse Value)> Run(string block, LineFixture facts, LineDeskContext? context = null)
    {
        var result = await new PlanDeskService().Handle(new PlanDeskRequest(block, facts, context ?? _context), CancellationToken.None);
        return result.IsError ? (true, result.Error.Code, null!) : (false, string.Empty, result.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Check_on_the_gold_plan_passes_with_nothing_to_write()
    {
        var (_, _, reply) = await Run("plan.check", LineFixture.Plan());

        Assert.Equal("step.plan.check", reply.Block);
        Assert.Equal("pass", reply.Outcome);
        Assert.Empty(reply.Outputs);
        Assert.Empty(reply.Files);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Check_with_gaps_writes_the_findings_fact()
    {
        var (_, _, reply) = await Run("step.plan.check", LineFixture.Plan().Edit("tasks.json", n => n["tasks"]![1]!["size"] = "L"));

        Assert.Equal("gaps", reply.Outcome);
        Assert.Equal("PLN-SIZE-001", (string?)JsonNode.Parse(reply.Outputs["findings"])![0]!["rule"]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Evidence_on_the_gold_plan_completes_with_the_candidate_packet_under_plan_packet()
    {
        var (_, _, reply) = await Run("step.plan.evidence", LineFixture.Plan());

        Assert.Equal("complete", reply.Outcome);
        Assert.Equal(["plan-packet/packet.json", "plan-packet/tasks.json", "plan-packet/index.json", "plan-packet/graph.json", "plan-packet/exec-review.json", "plan-packet/holdout.json", "plan-packet/answers.jsonl"], reply.Files.Select(f => f.Path), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_graph_is_derived_with_the_contexts_parallel()
    {
        var (_, _, reply) = await Run("step.plan.evidence", LineFixture.Plan(), _context with { Parallel = 1 });

        Assert.Equal("[[\"P1\"],[\"P2\"],[\"P3\"],[\"P4\"]]", JsonNode.Parse(reply.Files.Single(f => f.Path == "plan-packet/graph.json").Content)!["waves"]!.ToJsonString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Evidence_that_is_incomplete_writes_the_findings_and_no_packet()
    {
        var (_, _, reply) = await Run("step.plan.evidence", LineFixture.Plan().Edit("exec-review.json", n => n["phases"]![1]!["executable"] = false));

        Assert.Equal("incomplete", reply.Outcome);
        Assert.Empty(reply.Files);
        Assert.Contains(reply.Findings, f => f.Rule == "PLN-EXEC-001");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Evidence_with_no_identity_or_no_parent_hash_is_a_usage_error()
    {
        var (isError, code, _) = await Run("step.plan.evidence", LineFixture.Plan(), _context with { DesignHash = null });

        Assert.True(isError);
        Assert.Equal("SND-CLI-003", code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_block_this_station_does_not_have_is_a_usage_error()
    {
        var (isError, _, _) = await Run("step.plan.graph", LineFixture.Plan());

        Assert.True(isError);
    }
}
