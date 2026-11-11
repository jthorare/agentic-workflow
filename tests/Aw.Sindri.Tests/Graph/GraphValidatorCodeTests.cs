using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Validation;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.Graph;

/// <summary>The validator compiles every 'code' node (agent-first PR 9c), so a typo fails at save time with its line and column.</summary>
[Lane(TestLane.Unit)]
public sealed class GraphValidatorCodeTests
{
    private static GraphValidation Check(string code, Func<string, IReadOnlyList<CodeProblem>>? compile)
    {
        var blocks = new Dictionary<string, IGraphBlockType>(GraphFixtures.Blocks, StringComparer.Ordinal) { ["code"] = new CodeBlock(runner: null!) };   // why: validation never runs a node
        var node = new GraphNode(NodeId.From("filter"), "code", Title: null, new JsonObject { ["code"] = code });
        return Validator(blocks: blocks, codeCheck: compile).Validate(Spec([Trigger("daily"), node, Notify("tell", "hi")], [Edge("daily", "filter"), Edge("filter", "tell")]), GraphMode.Activate);
    }

    [Fact]
    public void A_code_node_that_does_not_compile_is_a_finding_with_its_line_and_column()
    {
        var result = Check("var n = 1;\nreturn n +;", static _ => [new CodeProblem(2, 11, "CS1525", "Invalid expression term ';'")]);

        var finding = Assert.Single(result.Findings, static f => f.Code == "GRAPH-CODE-001");
        Assert.Equal(["filter"], finding.Nodes);
        Assert.Equal("config.code", finding.Field);
        Assert.Contains("line 2, column 11", finding.Message, StringComparison.Ordinal);
        Assert.Contains("CS1525", finding.Message, StringComparison.Ordinal);
        Assert.False(result.Passed);
    }

    [Fact]
    public void A_code_node_that_compiles_and_a_context_without_a_compiler_both_pass()
    {
        Assert.True(Check("return 1;", static _ => []).Passed);
        Assert.True(Check("return 1;", compile: null).Passed);
    }

    [Fact]
    public void Braces_in_the_code_are_not_read_as_references()
    {
        var result = Check("return $\"{{not.a.reference}}\";", static _ => []);

        Assert.DoesNotContain(result.Findings, static f => f.Code.StartsWith("GRAPH-REF", StringComparison.Ordinal));
    }
}
