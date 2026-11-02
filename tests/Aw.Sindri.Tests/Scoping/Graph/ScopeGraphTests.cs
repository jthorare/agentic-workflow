using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Scoping.Graph;

/// <summary>Issue 1005, engine half: the scoping Steps get a prefiltered graph fact. SCP-GRAPH-001, the scope-map schema's nodes and origin, gather's fallback and the draft and challenge instructions.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeGraphTests
{
    private const string Graph = "SCP-GRAPH-001";
    private const string Schema = "SCP-SCHEMA-001";

    // The gold facts hold no fact:graph yet; this one holds exactly the map keys the gold surfaces name.
    private static ScopingFacts WithGraph()
    {
        var facts = ScopingFacts.Expiry();
        var keys = ScopeJson.Items(JsonNode.Parse(facts["scopeMap"]), "surfaces").Select(static s => ScopeJson.Str(s, "key")).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        facts["graph"] = new JsonObject
        {
            ["version"] = 1,
            ["mapVersion"] = "v13",
            ["anchors"] = new JsonArray(),
            ["depth"] = 2,
            ["hops"] = new JsonArray(),
            ["nodes"] = new JsonArray([.. keys.Select(static (k, i) => (JsonNode?)new JsonObject
            {
                ["id"] = k, ["kind"] = "feature", ["summary"] = k, ["rank"] = i + 1, ["lens"] = "product", ["lensItem"] = "L-product-" + (i + 1).ToString(CultureInfo.InvariantCulture), ["band"] = "product", ["citations"] = new JsonArray(),
            })]),
            ["edges"] = new JsonArray(),
            ["limits"] = new JsonArray(),
            ["size"] = new JsonObject { ["nodes"] = keys.Count, ["edges"] = 0, ["bytes"] = 0 },
        }.ToJsonString();
        return facts;
    }

    private static string FirstKey(ScopingFacts facts) => ScopeJson.Items(JsonNode.Parse(facts["scopeMap"]), "surfaces").Select(static s => ScopeJson.Str(s, "key")).OfType<string>().First();

    private static ScopingCheckResponse Run(ScopingFacts facts) => ScopePacketChecks.Run(new ScopeFacts(facts), ScopeStage.Draft);

    private static ScopeRuleStatus Status(ScopingCheckResponse response, string rule) => response.Rules.Single(r => r.Rule == rule).Status;

    private static ScopingFacts Surface(ScopingFacts facts, Action<JsonObject> edit) => facts.Edit("scopeMap", n => edit(n["surfaces"]![0]!.AsObject()));

    [Fact]
    public void A_surface_citing_nodes_that_are_all_in_the_graph_passes()
    {
        var facts = WithGraph();
        var key = FirstKey(facts);

        var response = Run(Surface(facts, s => { s["nodes"] = new JsonArray(key); s["origin"] = "graph"; }));

        Assert.Equal(ScopeRuleStatus.Pass, Status(response, Graph));
    }

    [Fact]
    public void A_surface_citing_a_node_the_graph_lacks_fails_and_names_it()
    {
        var response = Run(Surface(WithGraph(), s => { s["nodes"] = new JsonArray("feature:not-in-the-graph"); s["origin"] = "graph"; }));

        Assert.Equal(ScopeRuleStatus.Fail, Status(response, Graph));
        Assert.Contains(response.Findings, f => f.Rule == Graph && f.Message.Contains("feature:not-in-the-graph", StringComparison.Ordinal));
    }

    [Fact]
    public void A_graph_surface_with_no_node_fails()
    {
        var response = Run(Surface(WithGraph(), s => { s["nodes"] = new JsonArray(); s["origin"] = "graph"; }));

        Assert.Equal(ScopeRuleStatus.Fail, Status(response, Graph));
    }

    [Fact]
    public void A_surface_found_only_by_search_may_have_no_node()
    {
        var response = Run(Surface(WithGraph(), s => { s["nodes"] = new JsonArray(); s["origin"] = "search"; s["graphStatus"] = "unknown"; }));

        Assert.Equal(ScopeRuleStatus.Pass, Status(response, Graph));
    }

    [Fact]
    public void A_surface_that_names_a_key_must_list_it_among_its_nodes()
    {
        var response = Run(Surface(WithGraph(), s => { s["nodes"] = new JsonArray(); s["origin"] = "search"; }));

        Assert.Equal(ScopeRuleStatus.Fail, Status(response, Graph));
    }

    [Fact]
    public void Without_fact_graph_the_rule_is_not_run()
    {
        var response = Run(ScopingFacts.Expiry().Without("graph"));

        Assert.Equal(ScopeRuleStatus.NotRun, Status(response, Graph));
    }

    [Fact]
    public void The_scope_map_schema_accepts_nodes_and_origin()
    {
        var facts = WithGraph();
        var response = Run(Surface(facts, s => { s["nodes"] = new JsonArray(FirstKey(facts)); s["origin"] = "graph"; }));

        Assert.Equal(ScopeRuleStatus.Pass, Status(response, Schema));
    }

    [Fact]
    public void The_scope_map_schema_refuses_an_origin_other_than_graph_or_search()
    {
        var response = Run(Surface(WithGraph(), s => s["origin"] = "guess"));

        Assert.Equal(ScopeRuleStatus.Fail, Status(response, Schema));
    }

    [Fact]
    public async Task Gather_without_a_graph_view_still_gathers_and_writes_a_graph_with_a_recorded_limit()
    {
        var facts = new ScopingFacts { ["anchors"] = ScopingFacts.Expiry()["anchors"] };
        var inputs = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["input.context.lenses"] = JsonDocument.Parse(ScopingFacts.View("lensesView")).RootElement.Clone(),
            ["input.context.code"] = JsonDocument.Parse(ScopingFacts.View("codeView")).RootElement.Clone(),
            ["input.context.history"] = JsonDocument.Parse(ScopingFacts.View("historyView")).RootElement.Clone(),
        };

        var result = await new ScopeGatherStep().Run(ScopingStepContexts.Of("step.scope.gather", facts, inputs), TestContext.Current.CancellationToken);

        Assert.Equal("gathered", result.Result!.Outcome.Value);
        Assert.Equal(["coverage", "graph", "lenses"], ScopingStepContexts.Keys(result), StringComparer.Ordinal);
        var graph = JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:graph")].GetRawText())!;
        Assert.NotEmpty(ScopeJson.Items(graph, "limits"));
        var lensKeys = ScopeJson.Items(JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:lenses")].GetRawText())!["product"], "nodes").Select(static n => ScopeJson.Str(n, "key")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        Assert.True(lensKeys.IsSubsetOf(ScopeJson.Items(graph, "nodes").Select(static n => ScopeJson.Str(n, "id")).OfType<string>().ToHashSet(StringComparer.Ordinal)));
    }

    [Theory]
    [InlineData("step.scope.draft", typeof(ScopeDraftInstructions))]
    [InlineData("step.scope.challenge", typeof(ScopeChallengeInstructions))]
    public void The_draft_and_challenge_instructions_send_the_agent_to_the_graph_first(string step, Type type)
    {
        var instructions = (IAgentStepInstructions)Activator.CreateInstance(type, nonPublic: true)!;
        var text = string.Join('\n', instructions.Lines);

        Assert.True(instructions.IsFor(BlockId.From(step)));
        Assert.False(instructions.IsFor(BlockId.From("step.scope.gap-search")));
        Assert.Contains("graph.json", text, StringComparison.Ordinal);
        Assert.Contains("size", text, StringComparison.Ordinal);
        Assert.Contains("limits", text, StringComparison.Ordinal);
        Assert.Contains("context_node", text, StringComparison.Ordinal);
        Assert.Contains("context_neighbors", text, StringComparison.Ordinal);
        Assert.Contains("context_query", text, StringComparison.Ordinal);
        Assert.Contains("confirm", text, StringComparison.Ordinal);
        Assert.DoesNotContain("lenses.json", text, StringComparison.Ordinal);
    }
}
