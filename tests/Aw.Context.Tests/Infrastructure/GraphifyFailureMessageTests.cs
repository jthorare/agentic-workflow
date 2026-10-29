using Aw.Context.Core.Engine;
using Aw.Context.Infrastructure.Extract;
using Aw.Context.Infrastructure.Git;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>What the degraded-cluster reason says when graphify ends non-zero: the exit or the signal, and the end of its stderr (#1680).</summary>
[Trait("Lane", "integration")]
public sealed class GraphifyFailureMessageTests
{
    private static string Identity(string text) => text;

    private static async Task<string?> ReasonOf(string script)
    {
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal) { ["src/a.ts"] = "export const a = 1;\n" });
        using var stub = new StubGraphify(GraphifyOptions.Default.Pin);
        var options = stub.Options with { Sandbox = (_, command, _) => command.Contains("extract", StringComparer.Ordinal) ? ["/bin/sh", "-c", script, "sh"] : StubGraphify.RunThroughShell(command) };

        var set = await GraphifyRun.Clusters(new GraphifyClusterSource(new GitCli(), options), repo, ClusterLimits.Default);

        Assert.True(set.IsSuccess);
        Assert.False(set.Value.Available);
        return set.Value.Reason;
    }

    [Fact]
    public async Task A_failed_run_names_its_exit_code_and_the_last_line_of_stderr()
    {
        var reason = await ReasonOf("echo starting >&2; echo 'boom: parser crashed' >&2; exit 3");

        Assert.Contains("graphify exited with code 3", reason, StringComparison.Ordinal);
        Assert.EndsWith("starting | boom: parser crashed", reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_run_killed_by_a_signal_says_which()
    {
        var reason = await ReasonOf("echo 'about to die' >&2; kill -9 $$");

        Assert.StartsWith("graphify was killed by signal 9 (exit 137)", reason, StringComparison.Ordinal);
        Assert.EndsWith("about to die", reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_run_with_no_stderr_gives_the_bare_message()
    {
        var reason = await ReasonOf("exit 3");

        Assert.Equal("graphify exited with code 3", reason);
    }

    [Fact]
    public async Task Stderr_longer_than_the_cap_keeps_only_its_end()
    {
        var reason = await ReasonOf("i=0; while [ $i -lt 400 ]; do echo \"line $i of a very long complaint from graphify\" >&2; i=$((i+1)); done; echo THE-END >&2; exit 2");

        Assert.NotNull(reason);
        Assert.StartsWith("graphify exited with code 2: ", reason, StringComparison.Ordinal);
        Assert.EndsWith("THE-END", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("line 0 ", reason, StringComparison.Ordinal);
        Assert.True(reason.Length - "graphify exited with code 2: ".Length <= GraphifyFailure.MaxCharacters);
    }

    [Theory]
    [InlineData(1, "graphify exited with code 1")]
    [InlineData(128, "graphify exited with code 128")]
    [InlineData(129, "graphify was killed by signal 1 (exit 129)")]
    [InlineData(137, "graphify was killed by signal 9 (exit 137)")]
    [InlineData(143, "graphify was killed by signal 15 (exit 143)")]
    [InlineData(159, "graphify was killed by signal 31 (exit 159)")]
    [InlineData(160, "graphify exited with code 160")]
    public void The_signal_range_is_128_plus_N(int exit, string expected) =>
        Assert.Equal(expected, GraphifyFailure.Message(exit, string.Empty, Identity));

    [Fact]
    public void The_tail_is_the_last_five_non_empty_lines_joined_on_one_line()
    {
        var tail = GraphifyFailure.Tail("one\n\ntwo\r\nthree\n  \nfour\nfive\nsix\n", Identity);

        Assert.Equal("two | three | four | five | six", tail);
    }

    [Fact]
    public void The_tail_is_cut_to_its_last_500_characters_and_scrubbed()
    {
        var tail = GraphifyFailure.Tail(new string('a', 2000) + "XYZ", static text => text.Replace("XYZ", "[REDACTED]", StringComparison.Ordinal));

        Assert.Equal(GraphifyFailure.MaxCharacters, tail.Length);
        Assert.EndsWith("[REDACTED]", tail, StringComparison.Ordinal);
    }
}
