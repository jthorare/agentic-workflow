using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflow.Code;

namespace Aw.Sindri.Tests.Workflow.Code;

/// <summary>The Code block's compile step (agent-first PR 9c): Roslyn over a fixed reference set, errors in the body's own line and column. No process is started.</summary>
[Lane(TestLane.Unit)]
public sealed class CodeCompilerTests
{
    [Fact]
    public void A_body_with_a_typo_returns_a_diagnostic_with_line_and_column()
    {
        var result = new CodeCompiler().Compile("var n = 1;\nreturn n +;");

        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Diagnostics);
        Assert.Equal((2, 11), (error.Line, error.Column));   // the ';' that has no operand before it: line 2 of the body, column 11
        Assert.Equal("CS1525", error.Id);
    }

    [Fact]
    public void A_body_that_returns_a_value_compiles_to_an_assembly()
    {
        var result = new CodeCompiler().Compile("return input?[\"n\"]?.GetValue<int>() + 1;");

        Assert.True(result.Succeeded);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Linq_and_Json_are_in_the_reference_set_without_a_using()
    {
        var result = new CodeCompiler().Compile("return JsonSerializer.Serialize(new[] { 1, 2, 3 }.Where(static n => n > 1));");

        Assert.True(result.Succeeded);
    }
}
