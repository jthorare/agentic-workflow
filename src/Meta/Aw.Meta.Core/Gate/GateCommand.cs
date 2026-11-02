using Aw.Meta.Core.Config;

namespace Aw.Meta.Core.Gate;

/// <summary>Builds the argument vectors the gate runs.</summary>
public static class GateCommand
{
    private const string TestPlaceholder = "{test}";

    /// <summary>The step's command, through the heavy-job wrapper when the step is heavy and a wrapper is installed.</summary>
    /// <param name="step">The step.</param>
    /// <param name="heavy">The wrapper program, or null when none is installed.</param>
    /// <returns>The argument vector.</returns>
    public static IReadOnlyList<string> For(StepConfig step, string? heavy)
    {
        ArgumentNullException.ThrowIfNull(step);
        return step.Heavy && heavy is not null ? [heavy, .. step.Run] : step.Run;
    }

    /// <summary>The command that runs one test of the step: the step's rerun template (a word with text after {test} takes a test that already ends with that text unchanged, so "x", "dir/x.sh" and "other/x.sh" all fit "dir/{test}.sh"), else its own command with a method filter (dotnet test steps only).</summary>
    /// <param name="step">The step.</param>
    /// <param name="test">The test filter.</param>
    /// <param name="heavy">The wrapper program, or null when none is installed.</param>
    /// <returns>The argument vector, or null when the step has no way to run one test.</returns>
    public static IReadOnlyList<string>? Rerun(StepConfig step, string test, string? heavy)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(test);
        IReadOnlyList<string>? argv = step.Rerun is { Count: > 0 } template
            ? [.. template.Select(word => Fill(word, test))]
            : IsDotnetTest(step.Run) ? [.. step.Run, "--filter-method", "*" + test + "*"] : null;
        return argv is null ? null : step.Heavy && heavy is not null ? [heavy, .. argv] : argv;
    }

    // A test that already ends with the text after {test} (or, with none after it, starts with the text before) is a full name and is taken as is,
    // so a bare name and any full path both resolve, wherever the path lives.
    private static string Fill(string word, string test)
    {
        var at = word.IndexOf(TestPlaceholder, StringComparison.Ordinal);
        if (at < 0)
        {
            return word;
        }

        var prefix = word[..at];
        var suffix = word[(at + TestPlaceholder.Length)..];
        var wrapped = suffix.Length > 0 ? test.EndsWith(suffix, StringComparison.Ordinal) : prefix.Length > 0 && test.StartsWith(prefix, StringComparison.Ordinal);
        return wrapped ? test : word.Replace(TestPlaceholder, test, StringComparison.Ordinal);
    }

    private static bool IsDotnetTest(IReadOnlyList<string> run) =>
        run.Count > 1 && run[0] == "dotnet" && run.Contains("test", StringComparer.Ordinal);
}
