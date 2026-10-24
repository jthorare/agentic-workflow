namespace Aw.Judge.Tests.NotBuiltYet;

// Cutover spec §5.2: the verbs of units 6.3 and 6.4 answer the same way until they land: stdout empty, one stderr line
// 'judge: <verb> is not in this build yet', exit 1. The stub validates nothing, opens no database, reads no stdin and
// writes no file. The PR that lands a verb deletes its row here.
public sealed class NotBuiltYetCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Theory, Trait("Lane", "integration")]
    [InlineData("label", "label")]
    [InlineData("label status", "label")]
    [InlineData("label import --question ask-check --since 14d", "label")]
    [InlineData("label outcomes", "label")]
    [InlineData("label set item-1 skip", "label")]
    [InlineData("adjudicate", "adjudicate")]
    [InlineData("adjudicate ask-check --limit 60", "adjudicate")]
    [InlineData("eval", "eval")]
    [InlineData("eval ask-check --provider jev --labels any", "eval")]
    [InlineData("eval import-turns t.jsonl --since 14d", "eval import-turns")]
    [InlineData("eval import-turn t.jsonl", "eval")]
    [InlineData("prompt-sort why d1", "prompt-sort why")]
    [InlineData("prompt-sort import --since 14d", "prompt-sort import")]
    [InlineData("prompt-sort outcomes", "prompt-sort outcomes")]
    [InlineData("prompt-sort adjudicate --limit 80", "prompt-sort adjudicate")]
    [InlineData("prompt-sort eval", "prompt-sort eval")]
    [InlineData("prompt-sort promote --apply", "prompt-sort promote")]
    [InlineData("label --help", "label")]
    [InlineData("eval -h", "eval")]
    [InlineData("prompt-sort why --help", "prompt-sort why")]
    [InlineData("label status --providers bogus", "label")]
    [InlineData("--providers rules adjudicate", "adjudicate")]
    public async Task A_verb_that_is_not_built_says_so_and_exits_1(string commandLine, string verb)
    {
        var run = await new JudgeCli(_state).RunWithInput("not read", commandLine.Split(' '));

        Assert.Equal(new CliRun(1, "", $"judge: {verb} is not in this build yet\n"), run);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_stub_opens_no_database_and_writes_no_file()
    {
        var cli = new JudgeCli(_state);

        _ = await cli.Run("label", "status");
        _ = await cli.Run("eval", "import-turns", "t.jsonl");
        _ = await cli.Run("prompt-sort", "promote");

        Assert.Empty(Directory.GetFileSystemEntries(_state.Path));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_providers_flag_with_no_value_is_still_a_usage_error()
    {
        var run = await new JudgeCli(_state).Run("label", "status", "--providers");

        Assert.Equal(new CliRun(64, "", "usage: judge <command> [args] | judge <question> < input.json (--providers needs a comma-separated list)\n"), run);
    }

    public void Dispose() => _state.Dispose();
}
