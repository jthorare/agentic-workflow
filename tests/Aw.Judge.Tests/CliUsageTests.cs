namespace Aw.Judge.Tests;

// One exit-code scheme: a command line the judge can't read is a usage error, exit 64, nothing on stdout and one
// stderr line 'usage: judge <synopsis>' (with the problem in parentheses when there is one).
public sealed class CliUsageTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Theory, Trait("Lane", "integration")]
    [InlineData("usage: judge <command> [args] | judge <question> < input.json")]
    [InlineData("usage: judge health", "health", "extra")]
    [InlineData("usage: judge brief save|get|map-by-name|map-by-dispatch|set-agent-id …", "brief")]
    [InlineData("usage: judge brief save|get|map-by-name|map-by-dispatch|set-agent-id … (unknown subcommand 'list')", "brief", "list")]
    [InlineData("usage: judge brief save < input.json", "brief", "save", "extra")]
    [InlineData("usage: judge brief get <tool-use-id>", "brief", "get")]
    [InlineData("usage: judge brief map-by-name <name>", "brief", "map-by-name")]
    [InlineData("usage: judge brief map-by-dispatch <session-id> <prompt-id> <subagent-type>", "brief", "map-by-dispatch", "s1", "p1")]
    [InlineData("usage: judge brief set-agent-id <tool-use-id> <agent-id>", "brief", "set-agent-id", "toolu_a")]
    [InlineData("usage: judge why <decision-id>", "why")]
    [InlineData("usage: judge undo <decision-id>", "undo", "a", "b")]
    [InlineData("usage: judge approve <decision-id>", "approve")]
    [InlineData("usage: judge config get|set …", "config")]
    [InlineData("usage: judge config get|set … (unknown subcommand 'list')", "config", "list")]
    [InlineData("usage: judge config get", "config", "get", "extra")]
    public async Task A_command_line_the_judge_cant_read_is_a_usage_error(string stderr, params string[] args) =>
        Assert.Equal(new CliRun(64, "", stderr + "\n"), await new JudgeCli(_state).Run(args));

    [Fact, Trait("Lane", "integration")]
    public async Task An_unknown_first_word_is_an_unknown_question_whatever_stdin_holds()
    {
        var cli = new JudgeCli(_state);
        var usage = new CliRun(64, "", "usage: judge <command> [args] | judge <question> < input.json (unknown question 'no-such-question')\n");

        Assert.Equal(usage, await cli.RunWithInput("", "no-such-question"));
        Assert.Equal(usage, await cli.RunWithInput("{}", "no-such-question"));
    }

    public void Dispose() => _state.Dispose();
}
