using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Ops;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.Graph.Ops;

[Lane(TestLane.Unit)]
public sealed class ApplyOpsTests
{
    // daily -> a -> tell
    private static GraphSpec Base() => Spec(
        [Trigger("daily"), Search("a"), Notify("tell", "{{a.facts}}")],
        [Edge("daily", "a"), Edge("a", "tell")]);

    private static Result<GraphSpec> Apply(GraphSpec spec, params GraphOp[] ops) => GraphOps.Apply(spec, baseRevision: 4, currentRevision: 4, ops, Validator());

    private static NodeId Id(string id) => NodeId.From(id);

    [Fact]
    public void A_stale_base_revision_returns_a_conflict_with_the_current_revision()
    {
        var result = GraphOps.Apply(Base(), baseRevision: 3, currentRevision: 5, [new RemoveNodeOp(Id("tell"))], Validator());

        Assert.True(result.IsError);
        Assert.Equal("SND-GRAPH-007", result.Error.Code);
        Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
        Assert.Contains("revision 5", result.Error.Message, StringComparison.Ordinal);
        var extra = Assert.Single(result.Error.Extensions!);
        Assert.Equal("currentRevision", extra.Key);
        Assert.Equal(5, extra.Value.GetInt32());
    }

    [Fact]
    public void A_failing_op_applies_none_of_the_batch()
    {
        var spec = Base();

        var result = Apply(spec, new AddNodeOp(Id("extra"), "notify", Title: null, new JsonObject { ["title"] = "x" }), new ConnectOp(Id("a"), Id("ghost")));

        Assert.True(result.IsError);
        Assert.Equal("SND-GRAPH-012", result.Error.Code);
        Assert.Contains("op 2", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("ghost", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(3, spec.Nodes.Count);
        Assert.Equal(2, spec.Edges.Count);
    }

    [Fact]
    public void A_result_the_validator_blocks_is_refused_with_its_findings()
    {
        var result = Apply(Base(), new AddNodeOp(Id("odd"), "no_such_block"));

        Assert.True(result.IsError);
        Assert.Equal("SND-GRAPH-002", result.Error.Code);
        Assert.Contains("GRAPH-BLOCK-001", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_batch_is_refused()
    {
        var result = Apply(Base());

        Assert.Equal("SND-GRAPH-012", result.Error.Code);
    }

    [Fact]
    public void Add_node_appends_the_node_and_refuses_a_used_id()
    {
        var added = Apply(Base(), new AddNodeOp(Id("gate"), "condition", Title: "Is it new?", new JsonObject { ["when"] = "a.facts" }, OnFailure: "stop"));
        var again = Apply(Base(), new AddNodeOp(Id("a"), "memory_search", Title: null, new JsonObject { ["memory"] = "drift", ["query"] = "q" }));

        var node = added.Value.Nodes[^1];
        Assert.Equal("gate", node.Id.Value);
        Assert.Equal("condition", node.Type);
        Assert.Equal("Is it new?", node.Title);
        Assert.Equal("stop", node.OnFailure);
        Assert.Equal("a.facts", node.Config["when"]!.GetValue<string>());
        Assert.Equal("SND-GRAPH-012", again.Error.Code);
        Assert.Contains("'a'", again.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Set_config_sets_a_nested_path_creates_what_is_missing_and_leaves_the_input_alone()
    {
        var spec = Base();

        var result = Apply(spec, new SetConfigOp(Id("a"), "query", JsonValue.Create("newer")), new SetConfigOp(Id("tell"), "title", JsonValue.Create("Hi")));

        Assert.Equal("newer", result.Value.Nodes[1].Config["query"]!.GetValue<string>());
        Assert.Equal("last checked", spec.Nodes[1].Config["query"]!.GetValue<string>());
        var deep = Apply(spec, new AddNodeOp(Id("hook"), "webhook"), new SetConfigOp(Id("hook"), "url", JsonValue.Create("https://example.test")), new SetConfigOp(Id("hook"), "headers.X-Key", JsonValue.Create("v")));
        Assert.Equal("v", deep.Value.Nodes[^1].Config["headers"]!["X-Key"]!.GetValue<string>());
    }

    [Fact]
    public void Set_config_with_a_null_value_removes_the_key_and_a_path_through_a_non_object_is_refused()
    {
        var removed = Apply(Base(), new SetConfigOp(Id("tell"), "body", Value: null));
        var through = Apply(Base(), new SetConfigOp(Id("tell"), "title.deeper", JsonValue.Create(1)));
        var unknown = Apply(Base(), new SetConfigOp(Id("ghost"), "x", JsonValue.Create(1)));

        Assert.False(removed.Value.Nodes[2].Config.ContainsKey("body"));
        Assert.Equal("SND-GRAPH-012", through.Error.Code);
        Assert.Equal("SND-GRAPH-012", unknown.Error.Code);
    }

    [Fact]
    public void Remove_node_removes_its_edges_too()
    {
        var result = Apply(Base(), new RemoveNodeOp(Id("tell")));

        Assert.DoesNotContain(result.Value.Nodes, n => n.Id.Value == "tell");
        var edge = Assert.Single(result.Value.Edges);
        Assert.Equal("a", edge.To.Value);
        Assert.Equal("SND-GRAPH-012", Apply(Base(), new RemoveNodeOp(Id("ghost"))).Error.Code);
    }

    [Fact]
    public void Connect_adds_an_edge_with_its_branch_and_refuses_the_same_edge_twice()
    {
        var spec = Spec([Trigger("daily"), Condition("gate", "daily.scheduledFor"), Notify("tell", "hi")], [Edge("daily", "gate")]);

        var result = Apply(spec, new ConnectOp(Id("gate"), Id("tell"), "true"));
        var doubled = Apply(spec, new ConnectOp(Id("daily"), Id("gate")), new ConnectOp(Id("daily"), Id("gate"), "out"));

        var edge = result.Value.Edges[^1];
        Assert.Equal(("gate", "true", "tell"), (edge.From.Value, edge.Output, edge.To.Value));
        Assert.Equal("SND-GRAPH-012", doubled.Error.Code);
        Assert.Contains("op 1", doubled.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Disconnect_removes_one_edge_and_refuses_one_that_is_not_there()
    {
        var spec = Spec([Trigger("daily"), Search("a"), Notify("tell", "hi")], [Edge("daily", "a"), Edge("a", "tell")]);

        var result = Apply(spec, new DisconnectOp(Id("a"), Id("tell")), new ConnectOp(Id("daily"), Id("tell")));
        var missing = Apply(spec, new DisconnectOp(Id("daily"), Id("tell")));

        Assert.DoesNotContain(result.Value.Edges, e => e.From.Value == "a" && e.To.Value == "tell");
        Assert.Contains(result.Value.Edges, e => e.From.Value == "daily" && e.To.Value == "tell");
        Assert.Equal("SND-GRAPH-012", missing.Error.Code);
    }

    [Fact]
    public void Rename_follows_the_node_through_edges_and_references()
    {
        var result = Apply(Base(), new RenameOp(Id("a"), Id("recall")));

        Assert.Equal(["daily", "recall", "tell"], result.Value.Nodes.Select(n => n.Id.Value), StringComparer.Ordinal);
        Assert.Equal([("daily", "recall"), ("recall", "tell")], result.Value.Edges.Select(e => (e.From.Value, e.To.Value)));
        Assert.Equal("{{recall.facts}}", result.Value.Nodes[2].Config["body"]!.GetValue<string>());
    }

    [Fact]
    public void Rename_follows_the_node_into_a_condition_when_text_but_not_into_its_quoted_literals()
    {
        // the weekday-check template's shape: weekdays -> check -> gate(when check.flagged) -> tell
        var spec = Spec(
            [Trigger("weekdays"), Search("check"), Condition("gate", "check.flagged and note == \"check.flagged\" and recheck.flagged"), Notify("tell", "{{check.facts}}")],
            [Edge("weekdays", "check"), Edge("check", "gate"), Edge("gate", "tell", "true")]);

        var result = Apply(spec, new RenameOp(Id("check"), Id("inspect")));

        Assert.True(result.IsSuccess);
        Assert.Equal("inspect.flagged and note == \"check.flagged\" and recheck.flagged", result.Value.Nodes[2].Config["when"]!.GetValue<string>());
        Assert.Equal("{{inspect.facts}}", result.Value.Nodes[3].Config["body"]!.GetValue<string>());
    }

    [Fact]
    public void Rename_to_a_used_id_or_from_a_missing_node_is_refused()
    {
        Assert.Equal("SND-GRAPH-012", Apply(Base(), new RenameOp(Id("a"), Id("tell"))).Error.Code);
        Assert.Equal("SND-GRAPH-012", Apply(Base(), new RenameOp(Id("ghost"), Id("x"))).Error.Code);
    }

    [Fact]
    public void Set_meta_changes_only_the_fields_it_names_and_an_empty_text_clears()
    {
        var spec = Base() with { Description = "old", Concurrency = "one-per-workflow" };

        var result = Apply(spec, new SetMetaOp(Name: "Renamed", Description: ""));

        Assert.Equal("Renamed", result.Value.Name);
        Assert.Null(result.Value.Description);
        Assert.Equal("one-per-workflow", result.Value.Concurrency);
    }

    [Fact]
    public void Ops_read_and_write_as_JSON_with_an_op_member()
    {
        var parsed = GraphOpJson.Parse(
            """[{"op":"add_node","id":"gate","type":"condition","config":{"when":"a.facts"}},""" +
            """{"op":"set_config","node":"gate","path":"when","value":"a.other"},""" +
            """{"op":"remove_node","node":"tell"},""" +
            """{"op":"connect","from":"a","to":"gate","output":"out"},""" +
            """{"op":"disconnect","from":"a","to":"gate"},""" +
            """{"op":"rename","node":"a","to":"recall"},""" +
            """{"op":"set_meta","name":"X"}]""");

        Assert.True(parsed.IsSuccess);
        Assert.Collection(
            parsed.Value,
            op => Assert.IsType<AddNodeOp>(op),
            op => Assert.IsType<SetConfigOp>(op),
            op => Assert.IsType<RemoveNodeOp>(op),
            op => Assert.IsType<ConnectOp>(op),
            op => Assert.IsType<DisconnectOp>(op),
            op => Assert.IsType<RenameOp>(op),
            op => Assert.IsType<SetMetaOp>(op));
        var again = GraphOpJson.Parse(GraphOpJson.Write(parsed.Value));
        Assert.Equal(GraphOpJson.Write(parsed.Value), GraphOpJson.Write(again.Value));
    }

    [Theory]
    [InlineData("""[{"op":"explode","node":"a"}]""")]
    [InlineData("""[{"op":"remove_node"}]""")]
    [InlineData("""[{"op":"remove_node","node":"a","extra":1}]""")]
    [InlineData("""{"op":"remove_node","node":"a"}""")]
    [InlineData("not json")]
    public void Ops_that_are_not_known_ops_are_refused_with_the_op_error(string json)
    {
        var parsed = GraphOpJson.Parse(json);

        Assert.True(parsed.IsError);
        Assert.Equal("SND-GRAPH-012", parsed.Error.Code);
    }
}
