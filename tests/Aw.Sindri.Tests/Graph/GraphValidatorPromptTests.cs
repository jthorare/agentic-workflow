using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Validation;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.Graph;

[Lane(TestLane.Unit)]
public sealed class GraphValidatorPromptTests
{
    private const string Code = "GRAPH-PROMPT-001";

    [Fact]
    public void A_prompt_with_no_reference_straight_after_a_trigger_gets_a_warning()
    {
        var spec = Spec([Trigger("daily"), Prompt("ask", "Is main's CI red this morning?")], [Edge("daily", "ask")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        var finding = Assert.Single(result.Findings, f => f.Code == Code);
        Assert.Equal(GraphSeverity.Warning, finding.Severity);
        Assert.Equal(["ask"], finding.Nodes);
        Assert.Equal("config.prompt", finding.Field);
        Assert.Contains("'ask'", finding.ToString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(finding.Fix));
        Assert.True(result.Passed);
    }

    [Fact]
    public void A_prompt_that_references_an_upstream_node_gets_no_warning()
    {
        var spec = Spec([Trigger("daily"), Search("search"), Prompt("ask", "Summarize {{search.facts}}")], [Edge("daily", "search"), Edge("search", "ask")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        Assert.DoesNotContain(result.Findings, f => f.Code == Code);
    }

    [Fact]
    public void A_prompt_that_references_the_trigger_gets_no_warning()
    {
        var spec = Spec([Trigger("daily"), Prompt("ask", "Is {{daily.scheduledFor}} late?")], [Edge("daily", "ask")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        Assert.DoesNotContain(result.Findings, f => f.Code == Code);
    }

    [Fact]
    public void A_plain_prompt_after_a_non_trigger_node_gets_no_warning()
    {
        var spec = Spec([Trigger("daily"), Search("search"), Prompt("ask", "Is main's CI red this morning?")], [Edge("daily", "search"), Edge("search", "ask")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        Assert.DoesNotContain(result.Findings, f => f.Code == Code);
    }
}
