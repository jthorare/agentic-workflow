using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Expressions;
using Aw.Sindri.Core.Graph.Ids;
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
}
