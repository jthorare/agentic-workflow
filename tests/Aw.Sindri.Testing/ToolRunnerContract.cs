using Aw.Sindri.Infrastructure.Secrets;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every IToolRunner (the seam PointerSecrets runs 'security', 'secret-tool' and 'op' through) honours, run against the
/// scripted fake and the real ProcessToolRunner. Each case names a harmless command (printf, sh, sleep) the real runner
/// runs and the run it must end in; the fake binding answers with that run. So the outcomes the pointer tests script
/// on the fake (missing tool, timeout, exit code and output) are the ones the real runner produces.
/// </summary>
public abstract class ToolRunnerContract
{
    /// <summary>The runner under test; a scripted fake answers <paramref name="scripted"/>, the real one ignores it.</summary>
    /// <param name="scripted">The run the case expects.</param>
    /// <returns>The runner.</returns>
    internal abstract IToolRunner Create(ToolRun scripted);

    /// <summary>A tool that exits 0 returns its exit code and standard output, its arguments passed verbatim (no shell).</summary>
    [Fact]
    public void An_exit_returns_the_code_and_standard_output_with_arguments_verbatim()
    {
        var expected = new ToolRun(ToolOutcome.Exited, 0, "a b|$HOME *");

        var run = Create(expected).Run("printf", ["%s|%s", "a b", "$HOME *"], TimeSpan.FromSeconds(30));

        Expect.Equal(expected, run);
    }

    /// <summary>A non-zero exit is an exit with that code.</summary>
    [Fact]
    public void A_non_zero_exit_is_reported_with_its_code()
    {
        var expected = new ToolRun(ToolOutcome.Exited, 3, "");

        var run = Create(expected).Run("sh", ["-c", "exit 3"], TimeSpan.FromSeconds(30));

        Expect.Equal(expected, run);
    }

    /// <summary>Standard error is kept beside standard output, for a caller that classifies a failure by it.</summary>
    [Fact]
    public void An_exit_returns_standard_error_apart_from_standard_output()
    {
        var expected = new ToolRun(ToolOutcome.Exited, 1, "out\n", "permission denied\n");

        var run = Create(expected).Run("sh", ["-c", "echo out; echo 'permission denied' >&2; exit 1"], TimeSpan.FromSeconds(30));

        Expect.Equal(expected, run);
    }

    /// <summary>A tool not on PATH is Missing.</summary>
    [Fact]
    public void A_tool_not_on_path_is_missing()
        => Expect.Equal(ToolRun.Missing, Create(ToolRun.Missing).Run("aw-no-such-tool-7f3c2a", [], TimeSpan.FromSeconds(30)));

    /// <summary>A tool that runs past its timeout is TimedOut.</summary>
    [Fact]
    public void A_tool_past_its_timeout_is_timed_out()
        => Expect.Equal(ToolRun.TimedOut, Create(ToolRun.TimedOut).Run("sleep", ["30"], TimeSpan.FromMilliseconds(200)));
}
