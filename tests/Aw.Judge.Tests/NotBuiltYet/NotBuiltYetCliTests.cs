namespace Aw.Judge.Tests.NotBuiltYet;

// Cutover spec §5.2: the prompt-sort subcommands of unit 6.4 answer the same way until they land: stdout empty, one stderr line
// 'judge: <verb> is not in this build yet', exit 1. The stub validates nothing, opens no database, reads no stdin and
// writes no file. The PR that lands a verb deletes its row here.
public sealed class NotBuiltYetCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Theory, Trait("Lane", "integration")]
    [InlineData("prompt-sort why d1", "prompt-sort why")]
    [InlineData("prompt-sort import --since 14d", "prompt-sort import")]
    [InlineData("prompt-sort outcomes", "prompt-sort outcomes")]
    [InlineData("prompt-sort adjudicate --limit 80", "prompt-sort adjudicate")]
    [InlineData("prompt-sort eval", "prompt-sort eval")]
    [InlineData("prompt-sort promote --apply", "prompt-sort promote")]
    [InlineData("prompt-sort why --help", "prompt-sort why")]
    [InlineData("--providers rules prompt-sort adjudicate", "prompt-sort adjudicate")]
    public async Task A_verb_that_is_not_built_says_so_and_exits_1(string commandLine, string verb)
    {
        var run = await new JudgeCli(_state).RunWithInput("not read", commandLine.Split(' '));

        Assert.Equal(new CliRun(1, "", $"judge: {verb} is not in this build yet\n"), run);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_stub_opens_no_database_and_writes_no_file()
    {
        var cli = new JudgeCli(_state);

        _ = await cli.Run("prompt-sort", "why", "d1");
        _ = await cli.Run("prompt-sort", "promote");

        Assert.Empty(Directory.GetFileSystemEntries(_state.Path));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_providers_flag_with_no_value_is_still_a_usage_error()
    {
        var run = await new JudgeCli(_state).Run("prompt-sort", "why", "d1", "--providers");

        Assert.Equal(new CliRun(64, "", "usage: judge <command> [args] | judge <question> < input.json (--providers needs a comma-separated list)\n"), run);
    }

    public void Dispose() => _state.Dispose();
}
