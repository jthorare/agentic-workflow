using System.Text.Json.Nodes;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Features.Design;
using Aw.Sindri.Testing.Line;

namespace Aw.Sindri.Tests.Design;

/// <summary>The design block Steps run at the desk (line spec §4.1): 'check' reports pass or gaps, 'evidence' assembles the candidate packet or reports it incomplete.</summary>
public sealed class DesignDeskTests
{
    private static readonly LineDeskContext _context = new(new PacketIdentity("design-expiry", "fixture-labs/fixture-notes", "3288642cd47359fff2642b8bf429976915ca4894"), ScopeHash: "sha256:48b9dc81f8935c81f465f785a6e7dba0abd9697480786ad7994d1475da00c973");

    private static async Task<(bool IsError, string Code, LineDeskResponse Value)> Run(string block, LineFixture facts, LineDeskContext? context = null)
    {
        var result = await new DesignDeskService().Handle(new DesignDeskRequest(block, facts, context ?? _context), CancellationToken.None);
        return result.IsError ? (true, result.Error.Code, null!) : (false, string.Empty, result.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Check_on_the_gold_design_passes_with_nothing_to_write()
    {
        var (_, _, reply) = await Run("step.design.check", LineFixture.Design());

        Assert.Equal("step.design.check", reply.Block);
        Assert.Equal("pass", reply.Outcome);
        Assert.Empty(reply.Outputs);
        Assert.Empty(reply.Files);
        Assert.Empty(reply.Findings);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_short_name_resolves_to_the_step()
    {
        var (_, _, reply) = await Run("design.check", LineFixture.Design());

        Assert.Equal("step.design.check", reply.Block);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Check_with_gaps_writes_the_findings_fact()
    {
        var (_, _, reply) = await Run("step.design.check", LineFixture.Design().Edit("signals.json", n => n["signals"]!.AsObject().Remove("AC-2")));

        Assert.Equal("gaps", reply.Outcome);
        var findings = JsonNode.Parse(reply.Outputs["findings"])!.AsArray();
        Assert.Equal("DSN-ACC-001", (string?)findings[0]!["rule"]);
        Assert.Equal("AC-2", (string?)findings[0]!["item"]);
        Assert.Equal("fail", (string?)findings[0]!["severity"]);
        Assert.Contains(reply.Findings, f => f.Rule == "DSN-ACC-001");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Evidence_on_the_gold_design_completes_with_the_candidate_packet_under_design_packet()
    {
        var (_, _, reply) = await Run("step.design.evidence", LineFixture.Design());

        Assert.Equal("complete", reply.Outcome);
        Assert.Equal(13, reply.Files.Count);
        Assert.All(reply.Files, f => Assert.StartsWith("design-packet/", f.Path, StringComparison.Ordinal));
        Assert.Contains(reply.Files, f => f.Path == "design-packet/index.json");
        Assert.Empty(reply.Findings);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Evidence_carries_the_warnings_of_the_run()
    {
        var (_, _, reply) = await Run("step.design.evidence", LineFixture.Design().Edit("runs.json", n => n["runs"]![1]!["model"] = "fixture-writer-model"));

        Assert.Equal("complete", reply.Outcome);
        Assert.Contains(reply.Warnings, w => w.Rule == "DSN-LENS-002");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Evidence_that_is_incomplete_writes_the_findings_and_no_packet()
    {
        var (_, _, reply) = await Run("step.design.evidence", LineFixture.Design().EditText("lens-rounds.jsonl", t => t.Replace("Not changed: what the page shows beyond the fact is decision D2; the owner picks at stop 2.", string.Empty, StringComparison.Ordinal)));

        Assert.Equal("incomplete", reply.Outcome);
        Assert.Empty(reply.Files);
        Assert.Contains(reply.Findings, f => f.Rule == "DSN-LENS-003");
        Assert.True(reply.Outputs.ContainsKey("findings"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Evidence_with_no_identity_or_no_scope_hash_is_a_usage_error_naming_what_is_missing()
    {
        var (isError, code, _) = await Run("step.design.evidence", LineFixture.Design(), new LineDeskContext());

        Assert.True(isError);
        Assert.Equal("SND-CLI-003", code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_block_this_station_does_not_have_is_a_usage_error()
    {
        var (isError, code, _) = await Run("step.design.draft", LineFixture.Design());

        Assert.True(isError);
        Assert.Equal("SND-CLI-003", code);
    }
}
