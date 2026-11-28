using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Validation;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.Graph;

/// <summary>The spec's optional 'budget: { tokens }' (#3805): it reads and writes back as written, a spec without it keeps its hash, and a cap that is not a whole number above 0 is GRAPH-BUDGET-001.</summary>
[Lane(TestLane.Unit)]
public sealed class GraphBudgetSpecTests
{
    private const string WithBudget = """{"version":1,"name":"Budgeted","budget":{"tokens":200000},"nodes":[{"id":"start","type":"manual","config":{}}],"edges":[]}""";

    private static GraphSpec Simple() => Spec([Trigger("daily"), Prompt("ask", "Say hi")], [Edge("daily", "ask")]);

    [Fact]
    public void A_budget_reads_and_writes_back_as_written()
    {
        var spec = GraphJson.Parse(WithBudget).Value;

        Assert.Equal(200000L, spec.Budget?.Tokens);
        Assert.Contains("\"budget\":{\"tokens\":200000}", GraphJson.Write(spec), StringComparison.Ordinal);
        Assert.Equal(200000L, GraphJson.Parse(GraphJson.Write(spec)).Value.Budget?.Tokens);
    }

    [Fact]
    public void A_spec_without_a_budget_writes_no_budget_member()
    {
        var spec = Simple();

        Assert.Null(spec.Budget);
        Assert.DoesNotContain("budget", GraphJson.Write(spec), StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_member_inside_budget_is_refused()
    {
        var parsed = GraphJson.Parse("""{"version":1,"name":"B","budget":{"tokens":5,"dollars":3},"nodes":[],"edges":[]}""");

        Assert.True(parsed.IsError);
        Assert.Equal(SindriErrors.GraphInvalid.Code, parsed.Error.Code);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void A_budget_that_is_not_above_zero_is_GRAPH_BUDGET_001_on_the_budget_field(long tokens)
    {
        var result = Validator().Validate(Simple() with { Budget = new GraphBudget(tokens) }, GraphMode.Activate);

        var finding = Assert.Single(result.Findings, static f => string.Equals(f.Code, "GRAPH-BUDGET-001", StringComparison.Ordinal));
        Assert.Equal("budget.tokens", finding.Field);
        Assert.Equal(GraphSeverity.Error, finding.Severity);
        Assert.False(result.Passed);
    }

    [Fact]
    public void A_budget_with_no_tokens_is_GRAPH_BUDGET_001_too()
    {
        var result = Validator().Validate(Simple() with { Budget = new GraphBudget() }, GraphMode.Draft);

        Assert.Contains(result.Findings, static f => string.Equals(f.Code, "GRAPH-BUDGET-001", StringComparison.Ordinal));
    }

    [Fact]
    public void A_positive_budget_passes()
    {
        var result = Validator().Validate(Simple() with { Budget = new GraphBudget(1) }, GraphMode.Activate);

        Assert.DoesNotContain(result.Findings, static f => string.Equals(f.Code, "GRAPH-BUDGET-001", StringComparison.Ordinal));
    }
}
