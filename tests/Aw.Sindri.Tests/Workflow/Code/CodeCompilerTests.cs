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
        var result = new CodeCompiler().Compile("var n = 1;\nreturn n +;", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Diagnostics);
        Assert.Equal((2, 11), (error.Line, error.Column));   // the ';' that has no operand before it: line 2 of the body, column 11
        Assert.Equal("CS1525", error.Id);
    }

    [Fact]
    public void A_body_that_returns_a_value_compiles_to_an_assembly()
    {
        var result = new CodeCompiler().Compile("return input?[\"n\"]?.GetValue<int>() + 1;", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Linq_and_Json_are_in_the_reference_set_without_a_using()
    {
        var result = new CodeCompiler().Compile("return JsonSerializer.Serialize(new[] { 1, 2, 3 }.Where(static n => n > 1));", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void A_body_without_a_return_points_at_line_1_of_the_body_not_the_hidden_wrapper()
    {
        var result = new CodeCompiler().Compile("var x = 1;", TestContext.Current.CancellationToken);

        var error = Assert.Single(result.Diagnostics);
        Assert.Equal("CS0161", error.Id);   // not all code paths return a value: reported on the wrapper's Run, above the body
        Assert.Equal((1, 1), (error.Line, error.Column));
    }

    [Fact]
    public void The_fingerprint_names_the_wrapper_as_well_as_the_references()
    {
        var fingerprint = new CodeCompiler().Fingerprint;

        Assert.Contains("System.Text.Json@", fingerprint, StringComparison.Ordinal);
        Assert.Contains("wrapper@", fingerprint, StringComparison.Ordinal);   // a new using in the wrapper must miss the cache
    }
}
