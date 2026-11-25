using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Validation;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>Each event kind declares the fields it hands on and one sample; validate and the block catalog read that, not one shared field list (#3201).</summary>
[Lane(TestLane.Unit)]
public sealed class EventKindSchemasTests
{
    private static GraphValidator Validator() => GraphFixtures.Validator(blocks: GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}")).Types);

    private static JsonObject Config(string kind) => new() { ["connection"] = "github:work", ["event"] = kind };

    private static IEnumerable<string> Names(BlockOutputs outputs) => outputs.Fields.Keys.Select(static k => k.TrimEnd('?'));

    private static GraphSpec TriggerThenNotify(string kind, string reads) =>
        GraphFixtures.Spec(
            [new GraphNode(NodeId.From("hook"), "connection_event", Title: null, Config(kind)), GraphFixtures.Notify("tell", "got {{" + reads + "}}")],
            [GraphFixtures.Edge("hook", "tell")]);

    [Fact]
    public void Every_github_kind_has_a_schema_and_its_sample_has_exactly_the_schema_fields()
    {
        foreach (var kind in PollableEvents.For("github"))
        {
            var schema = EventKindSchemas.Find(kind);

            Assert.NotNull(schema);
            Assert.Equal(kind, schema.Kind);
            Assert.Equal(schema.Fields.Keys.Order(StringComparer.Ordinal), schema.Sample.Select(static p => p.Key).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        }
    }

    [Fact]
    public void A_kind_the_catalog_does_not_have_has_no_schema() => Assert.Null(EventKindSchemas.Find("github.pr.nope"));

    [Fact]
    public void The_checks_kind_lists_failing_as_an_array_and_the_push_kind_has_no_number()
    {
        var block = new ConnectionEventBlock();

        var checks = block.OutputsFor(Config("github.pr.checks"));
        var push = block.OutputsFor(Config("github.push"));

        Assert.Equal("array", checks.Fields["failing"]);
        Assert.DoesNotContain("number", Names(push), StringComparer.Ordinal);
        Assert.Contains("headSha", Names(push), StringComparer.Ordinal);
    }

    [Fact]
    public void A_push_trigger_read_for_a_pull_request_number_is_refused_on_the_reading_node()
    {
        var result = Validator().Validate(TriggerThenNotify("github.push", "trigger.number"), GraphMode.Activate);

        var finding = Assert.Single(result.Findings, static f => f.Code == "GRAPH-REF-002");
        Assert.Contains("tell", finding.Nodes, StringComparer.Ordinal);
    }

    [Fact]
    public void A_push_trigger_read_for_its_head_sha_passes()
    {
        var result = Validator().Validate(TriggerThenNotify("github.push", "trigger.headSha"), GraphMode.Activate);

        Assert.DoesNotContain(result.Findings, static f => f.Code.StartsWith("GRAPH-REF-", StringComparison.Ordinal));
    }

    [Fact]
    public void An_unknown_kind_is_still_refused_as_not_a_pollable_event()
    {
        var result = Validator().Validate(TriggerThenNotify("github.pr.nope", "trigger.title"), GraphMode.Activate);

        var finding = Assert.Single(result.Findings, static f => f.Code == "GRAPH-CONFIG-001" && f.Field == "config.event");
        Assert.Contains("github.pr.nope", finding.ToString(), StringComparison.Ordinal);
    }
}
