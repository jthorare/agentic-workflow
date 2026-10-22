using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>A profile command line becomes argv with no shell (line spec LN-WF9, decision A): spaces split, "double quotes" group.</summary>
[Lane(TestLane.Unit)]
public sealed class CommandLineTests
{
    [Theory]
    [InlineData("dotnet test", new[] { "dotnet", "test" })]
    [InlineData("  dotnet \t test  -c   Release ", new[] { "dotnet", "test", "-c", "Release" })]
    [InlineData("dotnet test --filter \"Lane=unit & Fast\"", new[] { "dotnet", "test", "--filter", "Lane=unit & Fast" })]
    [InlineData("tool --name=\"a b\" tail", new[] { "tool", "--name=a b", "tail" })]
    [InlineData("tool \"\" x", new[] { "tool", "", "x" })]
    [InlineData("\"my tool\" run", new[] { "my tool", "run" })]
    [InlineData("echo $HOME | cat > out.txt *", new[] { "echo", "$HOME", "|", "cat", ">", "out.txt", "*" })]
    [InlineData("tool 'a b'", new[] { "tool", "'a", "b'" })]
    public void A_line_splits_on_spaces_and_double_quoted_groups_with_no_shell(string line, string[] expected)
    {
        Assert.True(CommandLine.TryParse(line, out var argv, out var problem));

        Assert.Equal(expected, argv);
        Assert.Null(problem);
    }

    [Theory]
    [InlineData("dotnet test --filter \"a", "never closed")]
    [InlineData("tool \"a \"\"b", "never closed")]
    [InlineData("", "no program")]
    [InlineData("   \t ", "no program")]
    [InlineData("\"\" run", "no program")]
    public void An_unbalanced_quote_or_a_line_with_no_program_is_a_problem_with_a_reason(string line, string reason)
    {
        Assert.False(CommandLine.TryParse(line, out var argv, out var problem));

        Assert.Empty(argv);
        Assert.Contains(reason, problem, StringComparison.Ordinal);
    }
}
