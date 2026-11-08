using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Expressions;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Schedule;
using Aw.Sindri.Core.Graph.Validation;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.Graph;

[Lane(TestLane.Unit)]
public sealed class GraphValidatorTests
{
    [Fact]
    public void A_spec_with_two_triggers_is_refused_naming_both_nodes()
    {
        var spec = Spec([Trigger("daily"), Trigger("hourly"), Notify("tell", "hi")], [Edge("daily", "tell"), Edge("hourly", "tell")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        var finding = Assert.Single(result.Findings, f => f.Code == "GRAPH-TRIGGER-001");
        Assert.Equal(["daily", "hourly"], finding.Nodes);
        Assert.Contains("'daily'", finding.ToString(), StringComparison.Ordinal);
        Assert.Contains("'hourly'", finding.ToString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(finding.Fix));
        Assert.False(result.Passed);
    }

    [Fact]
    public void A_reference_to_a_node_that_is_not_upstream_is_refused()
    {
        // daily -> a -> c, and daily -> b: b is a sibling branch of c, and c is downstream of a
        var spec = Spec(
            [Trigger("daily"), Search("a", "after {{c.facts}}"), Search("b"), Search("c", "before {{b.facts}}")],
            [Edge("daily", "a"), Edge("daily", "b"), Edge("a", "c")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        var refused = result.Findings.Where(f => f.Code == "GRAPH-REF-001").ToList();
        Assert.Equal(2, refused.Count);
        var downstream = Assert.Single(refused, f => f.Nodes.SequenceEqual(["a"], StringComparer.Ordinal));
        Assert.Equal("config.query", downstream.Field);
        Assert.Contains("'c'", downstream.ToString(), StringComparison.Ordinal);
        var sibling = Assert.Single(refused, f => f.Nodes.SequenceEqual(["c"], StringComparer.Ordinal));
        Assert.Contains("'b'", sibling.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(result.Findings, f => f.Code == "GRAPH-REF-002");
    }

    [Fact]
    public void A_cycle_is_refused_with_the_path()
    {
        var spec = Spec(
            [Trigger("daily"), Search("a"), Search("b"), Search("c")],
            [Edge("daily", "a"), Edge("a", "b"), Edge("b", "c"), Edge("c", "a")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        var cycle = Assert.Single(result.Findings, f => f.Code == "GRAPH-CYCLE-001");
        Assert.Equal(["a", "b", "c"], cycle.Nodes);
        Assert.Contains("a -> b -> c -> a", cycle.ToString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(cycle.Fix));
    }

    [Fact]
    public void A_draft_may_have_wiring_gaps_but_activation_refuses_every_finding()
    {
        var unconnected = Spec([Trigger("daily"), Search("a"), Search("lonely")], [Edge("daily", "a")]);
        var noTrigger = Spec([Search("a"), Search("b")], [Edge("a", "b")]);

        foreach (var (spec, code) in new[] { (unconnected, "GRAPH-WIRING-001"), (noTrigger, "GRAPH-TRIGGER-002") })
        {
            var draft = Validator().Validate(spec, GraphMode.Draft);
            var activate = Validator().Validate(spec, GraphMode.Activate);

            var gap = Assert.Single(draft.Findings);
            Assert.Equal(code, gap.Code);
            Assert.True(gap.WiringGap);
            Assert.True(draft.Passed);
            Assert.False(activate.Passed);
            Assert.Equal(activate.Findings.Count, activate.Blocking.Count);
        }
    }

    [Fact]
    public void A_draft_still_refuses_what_is_not_a_wiring_gap()
    {
        var odd = new GraphNode(NodeId.From("odd"), "teleport", Title: null, Config: JsonNode.Parse("{}")!.AsObject());
        var spec = Spec([Trigger("daily"), odd], [Edge("daily", "odd")]);

        var draft = Validator().Validate(spec, GraphMode.Draft);

        var unknown = Assert.Single(draft.Findings, f => f.Code == "GRAPH-BLOCK-001");
        Assert.Equal(["odd"], unknown.Nodes);
        Assert.False(draft.Passed);
    }

    [Fact]
    public void A_bad_cron_or_timezone_is_refused_naming_the_field()
    {
        var spec = Spec([Trigger("daily")], []);
        static string? Problem(string cron, string? zone) => cron.Split(' ').Length != 5 ? "cron needs five fields" : zone == "Mars/Base" ? "unknown timezone 'Mars/Base'" : null;

        var badCron = Validator(cron: Problem).Validate(Spec([new GraphNode(NodeId.From("daily"), "schedule", Title: null, Config: JsonNode.Parse("""{"cron":"* *"}""")!.AsObject())], []), GraphMode.Activate);
        var badZone = Validator(cron: Problem).Validate(Spec([new GraphNode(NodeId.From("daily"), "schedule", Title: null, Config: JsonNode.Parse("""{"cron":"0 7 * * *","timezone":"Mars/Base"}""")!.AsObject())], []), GraphMode.Activate);

        foreach (var result in new[] { badCron, badZone })
        {
            var finding = Assert.Single(result.Findings, f => f.Code == "GRAPH-CRON-001");
            Assert.Equal(["daily"], finding.Nodes);
            Assert.Equal("config.cron", finding.Field);
        }

        Assert.Contains("five fields", badCron.Findings[0].Message, StringComparison.Ordinal);
        Assert.Contains("Mars/Base", badZone.Findings[0].Message, StringComparison.Ordinal);
        Assert.Empty(Validator(cron: Problem).Validate(spec, GraphMode.Activate).Findings);
    }

    private static GraphValidator WithConditions() => Validator(condition: text =>
        ConditionExpression.TryParse(text, out var expression, out var error) ? new ConditionParse(Error: null, expression!.References) : new ConditionParse(error, []));   // why: TryParse sets the expression when it returns true

    private static GraphNode When(string id, string when) =>
        new(NodeId.From(id), "condition", Title: null, Config: new JsonObject { ["when"] = when });

    [Fact]
    public void A_condition_that_does_not_parse_is_refused_with_the_parsers_text()
    {
        var spec = Spec([Trigger("daily"), When("if", "{{a.facts}} =="), Notify("tell", "hi")], [Edge("daily", "if"), Edge("if", "tell", "true")]);

        var result = WithConditions().Validate(spec, GraphMode.Activate);

        var finding = Assert.Single(result.Findings, f => f.Code == "GRAPH-COND-001");
        Assert.Equal(["if"], finding.Nodes);
        Assert.Equal("config.when", finding.Field);
        Assert.Contains("can't be read", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_condition_that_reads_a_node_that_is_not_upstream_is_refused()
    {
        var spec = Spec(
            [Trigger("daily"), Search("a"), Search("b"), When("if", "{{b.facts}} == \"x\" and exists(a.facts)")],
            [Edge("daily", "a"), Edge("daily", "b"), Edge("a", "if")]);

        var result = WithConditions().Validate(spec, GraphMode.Activate);

        var finding = Assert.Single(result.Findings, f => f.Code == "GRAPH-REF-001");
        Assert.Equal(["if"], finding.Nodes);
        Assert.Equal("config.when", finding.Field);
        Assert.Contains("'b'", finding.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void An_edge_to_or_from_a_node_that_does_not_exist_is_refused()
    {
        var spec = Spec([Trigger("daily"), Notify("tell", "hi")], [Edge("daily", "tell"), Edge("daily", "ghost"), Edge("phantom", "tell")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        var edges = result.Findings.Where(f => f.Code == "GRAPH-EDGE-001").ToList();
        Assert.Equal(2, edges.Count);
        Assert.Contains(edges, f => f.Nodes.SequenceEqual(["ghost"], StringComparer.Ordinal));
        Assert.Contains(edges, f => f.Nodes.SequenceEqual(["phantom"], StringComparer.Ordinal));
    }

    [Fact]
    public void An_edge_that_leaves_a_block_by_an_output_it_does_not_have_is_refused()
    {
        var spec = Spec([Trigger("daily"), Search("a"), When("if", "{{a.facts}}"), Notify("tell", "hi")], [Edge("daily", "a"), Edge("a", "if"), Edge("if", "tell", "maybe")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        var finding = Assert.Single(result.Findings, f => f.Code == "GRAPH-EDGE-002");
        Assert.Equal(["if"], finding.Nodes);
        Assert.Contains("'maybe'", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_edge_with_no_output_out_of_a_branching_block_is_refused()
    {
        var spec = Spec([Trigger("daily"), Search("a"), When("if", "{{a.facts}}"), Notify("tell", "hi")], [Edge("daily", "a"), Edge("a", "if"), Edge("if", "tell")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        var finding = Assert.Single(result.Findings, f => f.Code == "GRAPH-EDGE-002");
        Assert.Equal(["if"], finding.Nodes);
        Assert.Contains("'out'", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_node_id_used_twice_is_refused()
    {
        var spec = Spec([Trigger("daily"), Notify("tell", "one"), Notify("tell", "two")], [Edge("daily", "tell")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        var finding = Assert.Single(result.Findings, f => f.Code == "GRAPH-NODE-001");
        Assert.Equal(["tell"], finding.Nodes);
    }

    [Theory]
    [InlineData("trigger")]
    [InlineData("item")]
    [InlineData("workflow")]
    public void A_node_named_like_a_reserved_reference_root_is_refused(string id)
    {
        var spec = Spec([Trigger("daily"), Search(id), Notify("tell", "{{" + id + ".nope}}")], [Edge("daily", id), Edge(id, "tell")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        var finding = Assert.Single(result.Findings, f => f.Code == "GRAPH-NODE-002");
        Assert.Equal([id], finding.Nodes);
        Assert.Contains("trigger", finding.Message, StringComparison.Ordinal);
        Assert.Contains("item", finding.Message, StringComparison.Ordinal);
        Assert.Contains("workflow", finding.Message, StringComparison.Ordinal);
        Assert.Null(finding.Field);
    }

    [Fact]
    public void A_config_that_does_not_fit_its_block_is_refused_naming_the_field()
    {
        var odd = new GraphNode(NodeId.From("tell"), "notify", Title: null, Config: new JsonObject { ["title"] = 3, ["colour"] = "red" });
        var spec = Spec([Trigger("daily"), odd], [Edge("daily", "tell")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        var fields = result.Findings.Where(f => f.Code == "GRAPH-CONFIG-001").Select(static f => f.Field).ToList();
        Assert.Equal(["config.title", "config.colour"], fields);
    }

    [Fact]
    public void A_reference_to_an_output_the_node_does_not_have_is_refused()
    {
        var spec = Spec([Trigger("daily"), Search("a"), Notify("tell", "{{a.nope}}")], [Edge("daily", "a"), Edge("a", "tell")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        var finding = Assert.Single(result.Findings, f => f.Code == "GRAPH-REF-002");
        Assert.Equal(["tell"], finding.Nodes);
        Assert.Contains("facts", finding.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void A_reference_to_a_node_that_does_not_exist_is_refused()
    {
        var spec = Spec([Trigger("daily"), Notify("tell", "{{ghost.facts}}")], [Edge("daily", "tell")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        var finding = Assert.Single(result.Findings, f => f.Code == "GRAPH-REF-003");
        Assert.Equal("config.body", finding.Field);
    }

    [Fact]
    public void A_reference_inside_a_nested_object_or_a_list_is_checked_and_names_its_path()
    {
        var hook = Hook("call", new JsonObject { ["headers"] = new JsonObject { ["auth"] = "{{ghost.token}}" }, ["args"] = new JsonArray("ok", "{{phantom.x}}") });
        var spec = Spec([Trigger("daily"), hook], [Edge("daily", "call")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        var fields = result.Findings.Where(f => f.Code == "GRAPH-REF-003").Select(static f => f.Field).ToList();
        Assert.Equal(["config.headers.auth", "config.args[1]"], fields);
    }

    [Fact]
    public void An_unclosed_reference_is_refused_naming_the_node_and_field()
    {
        var spec = Spec([Trigger("daily"), Agent("agent", "web"), Notify("tell", "drifted: {{agent.drifted")], [Edge("daily", "agent"), Edge("agent", "tell")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        var finding = Assert.Single(result.Findings, f => f.Code == "GRAPH-REF-004");
        Assert.Equal(["tell"], finding.Nodes);
        Assert.Equal("config.body", finding.Field);
        Assert.False(result.Passed);
    }

    [Fact]
    public void A_repo_the_profile_does_not_have_is_refused_and_one_it_cannot_check_yet_is_a_warning()
    {
        var spec = Spec([Trigger("daily"), Agent("agent", "nowhere")], [Edge("daily", "agent")]);

        var unknown = Validator(new RepoWorld(GraphLookup.Unknown)).Validate(spec, GraphMode.Activate);
        var unavailable = Validator(new RepoWorld(GraphLookup.Unavailable)).Validate(spec, GraphMode.Activate);

        var error = Assert.Single(unknown.Findings, f => f.Code == "GRAPH-REPO-001");
        Assert.Equal(GraphSeverity.Error, error.Severity);
        Assert.Equal("config.repo", error.Field);
        Assert.False(unknown.Passed);
        var warning = Assert.Single(unavailable.Findings, f => f.Code == "GRAPH-REPO-002");
        Assert.Equal(GraphSeverity.Warning, warning.Severity);
        Assert.True(unavailable.Passed);
    }

    [Fact]
    public void A_schedule_with_more_than_six_crons_is_refused()
    {
        var crons = new JsonArray(Enumerable.Range(0, 7).Select(static i => (JsonNode)JsonValue.Create(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{i} 7 * * *"))).ToArray());
        var many = new GraphNode(NodeId.From("daily"), "schedule", Title: null, Config: new JsonObject { ["cron"] = crons });

        var result = Validator().Validate(Spec([many], []), GraphMode.Activate);

        Assert.Contains(result.Findings, f => f.Code == "GRAPH-CRON-001" && f.Message.Contains("up to 6", StringComparison.Ordinal));
    }

    [Fact]
    public void The_real_cron_reader_refuses_a_bad_cron_and_a_bad_timezone_and_passes_a_good_one()
    {
        static string? Real(string cron, string? zone) => CronExpression.TryParse(cron, zone, out _, out var problem) ? null : problem;
        static GraphNode At(string cron, string zone) => new(NodeId.From("daily"), "schedule", Title: null, Config: new JsonObject { ["cron"] = cron, ["timezone"] = zone });

        var bad = Validator(cron: Real).Validate(Spec([At("61 7 * * *", "America/Los_Angeles")], []), GraphMode.Activate);
        var badZone = Validator(cron: Real).Validate(Spec([At("0 7 * * *", "Mars/Base")], []), GraphMode.Activate);
        var good = Validator(cron: Real).Validate(Spec([At("0 7 * * 1-5", "America/Los_Angeles")], []), GraphMode.Activate);

        _ = Assert.Single(bad.Findings, f => f.Code == "GRAPH-CRON-001");
        _ = Assert.Single(badZone.Findings, f => f.Code == "GRAPH-CRON-001");
        Assert.DoesNotContain(good.Findings, f => f.Code == "GRAPH-CRON-001");
    }
}
