using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Features.Plan;
using Aw.Sindri.Testing.Line;

namespace Aw.Sindri.Tests.Plan;

/// <summary>The plan packet assembler (line spec §3.2 row 3, §6.2): the gold plan assembles back into the gold packet, with graph.json derived by code from the tasks.</summary>
public sealed class PlanPacketTests
{
    private const string DesignHash = "sha256:3deb049307bf38e0981ed4ed374f7d84a63f305f0067a247c10f839dfd4f87e4";
    private const string ScopeHash = "sha256:48b9dc81f8935c81f465f785a6e7dba0abd9697480786ad7994d1475da00c973";
    private static readonly PacketIdentity _identity = new("line-expiry-plan", "fixture-labs/fixture-notes", "3288642cd47359fff2642b8bf429976915ca4894");
    private static readonly PlanParents _parents = new(DesignHash, ScopeHash);

    private static Dictionary<string, string> Built(LineFixture facts, PlanGraphOptions? options = null) =>
        PlanPacket.Build(new LineFacts(facts), _identity, _parents, options ?? new PlanGraphOptions()).ToDictionary(static f => f.Path, static f => f.Content, StringComparer.Ordinal);

    private static JsonNode WithoutBaseNote(string graph)
    {
        var node = JsonNode.Parse(graph)!;
        foreach (var phase in node["phases"]!.AsArray())
        {
            _ = phase!.AsObject().Remove("baseNote");
        }

        return node;
    }

    [Fact, Trait("Lane", "unit")]
    public void The_gold_plan_assembles_back_into_the_gold_packet_byte_for_byte_except_graph_json()
    {
        var gold = LineFixture.Plan();

        var built = Built(gold);

        Assert.Equal(["packet.json", "tasks.json", "index.json", "graph.json", "exec-review.json", "holdout.json", "answers.jsonl"], PlanPacket.Build(new LineFacts(gold), _identity, _parents, new PlanGraphOptions()).Select(f => f.Path), StringComparer.Ordinal);
        foreach (var name in new[] { "packet.json", "tasks.json", "index.json", "exec-review.json", "holdout.json", "answers.jsonl" })
        {
            Assert.Equal(gold[name], built[name]);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_graph_derived_from_the_gold_tasks_is_the_gold_graph_without_its_hand_written_note()
    {
        var gold = LineFixture.Plan();

        var derived = JsonNode.Parse(Built(gold)["graph.json"])!;

        Assert.True(JsonNode.DeepEquals(WithoutBaseNote(gold["graph.json"]), derived), derived.ToJsonString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_assembled_packet_completes_the_evidence_checks()
    {
        var facts = LineFixture.Plan();
        foreach (var (name, content) in Built(facts))
        {
            facts[name] = content;
        }

        var response = await PlanCheckTests.Run(facts, LineStage.Evidence);

        Assert.Equal(LineOutcome.Complete, response.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void Index_json_lists_the_design_then_the_scope_and_extends_the_design_acceptance_with_no_acceptance_of_its_own()
    {
        var facts = LineFixture.Plan();

        var index = JsonNode.Parse(Built(facts)["index.json"])!;

        Assert.Equal("plan", (string?)index["kind"]);
        Assert.Equal([DesignHash, ScopeHash], index["parents"]!.AsArray().Select(p => (string?)p), StringComparer.Ordinal);
        Assert.Null(index["acceptance"]!["self"]);
        Assert.Equal("sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(facts["design/acceptance.json"]))), (string?)index["acceptance"]!["extends"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_graph_the_planner_wrote_is_replaced_by_the_derived_one()
    {
        var facts = LineFixture.Plan().Edit("graph.json", n => n["prCount"] = 99);

        var graph = JsonNode.Parse(Built(facts)["graph.json"])!;

        Assert.Equal(4, (int)graph["prCount"]!);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_waves_split_at_the_profiles_parallel()
    {
        var graph = JsonNode.Parse(Built(LineFixture.Plan(), new PlanGraphOptions(Parallel: 1))["graph.json"])!;

        Assert.Equal("[[\"P1\"],[\"P2\"],[\"P3\"],[\"P4\"]]", graph["waves"]!.ToJsonString());
        Assert.Equal(1, (int)graph["parallel"]!);
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_phases_with_no_dependency_start_two_stacks_from_the_default_branch()
    {
        var tasks = "{\"workstreams\":[],\"tasks\":[{\"id\":\"P1\",\"deps\":[]},{\"id\":\"P2\",\"deps\":[]}]}";

        var graph = JsonNode.Parse(PlanGraph.Derive(tasks, new PlanGraphOptions(DefaultBranch: "trunk"))!)!;

        Assert.Equal([1, 2], graph["phases"]!.AsArray().Select(p => (int)p!["stack"]!));
        Assert.All(graph["phases"]!.AsArray(), p => { Assert.Equal("default-branch", (string?)p!["base"]!["rule"]); Assert.Equal("trunk", (string?)p["base"]!["ref"]); });
        Assert.Equal("[[\"P1\",\"P2\"]]", graph["waves"]!.ToJsonString());
        Assert.Equal(["P1"], graph["criticalPath"]!.AsArray().Select(p => (string?)p), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_critical_path_is_the_longest_chain_and_a_phase_on_two_dependencies_waits_for_merge()
    {
        var graph = JsonNode.Parse(Built(LineFixture.Plan())["graph.json"])!;

        Assert.Equal(["P1", "P2", "P4"], graph["criticalPath"]!.AsArray().Select(p => (string?)p), StringComparer.Ordinal);
        Assert.Equal([false, false, false, true], graph["phases"]!.AsArray().Select(p => (bool)p!["waitsForMerge"]!));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_dependency_loop_derives_without_looping()
    {
        var tasks = "{\"tasks\":[{\"id\":\"P1\",\"deps\":[\"P2\"]},{\"id\":\"P2\",\"deps\":[\"P1\"]}]}";

        Assert.NotNull(PlanGraph.Derive(tasks, new PlanGraphOptions()));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"tasks\":3}")]
    public void Tasks_that_are_no_task_plan_derive_nothing(string tasks) => Assert.Null(PlanGraph.Derive(tasks, new PlanGraphOptions()));
}
