namespace Aw.Judge.Tests;

// Storage fails open (docs/csharp/judge.md "Fail-open"): when decisions.sqlite can't be opened or written, a question
// still prints its decision with its normal exit code (the write is skipped), the brief commands report one stderr
// line and exit 1 (done-gate.sh fails open on anything but "no brief for"; the other hooks ignore it), and health exits
// 1 so judge-health.sh says "health check failed".
public sealed class UnusableStorageTests : IDisposable
{
    private const string Brief =
        """{"toolUseId":"toolu_9","sessionId":"s","promptId":"p","dispatchName":null,"subagentType":"Explore","goal":"g","acceptanceCriteria":"a","proofCommand":"c"}""";

    private readonly TempStateDirectory _state = new();

    public enum Storage
    {
        /// <summary>$AW_STATE_DIR/judge exists, empty and read-only: the database can't be created.</summary>
        ReadOnlyDirectory,

        /// <summary>The database exists, but it and its directory are read-only: no write, no WAL file.</summary>
        ReadOnlyDatabase,

        /// <summary>decisions.sqlite holds bytes that aren't a SQLite database.</summary>
        CorruptFile,
    }

    private string JudgeDirectory => Path.Combine(_state.Path, "judge");

    private async Task<JudgeCli> Broken(Storage storage)
    {
        var cli = new JudgeCli(_state);
        switch (storage)
        {
            case Storage.ReadOnlyDirectory:
                _ = Directory.CreateDirectory(JudgeDirectory);
                break;
            case Storage.ReadOnlyDatabase:
                Assert.Equal(0, (await cli.Run("health")).ExitCode);   // creates the database
                Mode(Path.Combine(JudgeDirectory, "decisions.sqlite"), UnixFileMode.UserRead);
                break;
            case Storage.CorruptFile:
                _ = Directory.CreateDirectory(JudgeDirectory);
                await File.WriteAllTextAsync(Path.Combine(JudgeDirectory, "decisions.sqlite"), "this is not a database, just text\n", TestContext.Current.CancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(storage), storage, "unknown storage shape");
        }

        if (storage != Storage.CorruptFile)
        {
            Mode(JudgeDirectory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }

        return cli;
    }

    // The tests run on macOS and Linux (CI); Windows has no Unix mode bits to take away.
    private static void Mode(string path, UnixFileMode mode)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, mode);
        }
    }

    private static void OneErrorLine(CliRun run)
    {
        Assert.Equal((1, ""), (run.ExitCode, run.Stdout));
        Assert.EndsWith("\n", run.Stderr, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', run.Stderr.TrimEnd('\n'));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData(Storage.ReadOnlyDirectory)]
    [InlineData(Storage.ReadOnlyDatabase)]
    [InlineData(Storage.CorruptFile)]
    public async Task A_question_still_prints_its_decision_with_its_exit_code(Storage storage)
    {
        var cli = await Broken(storage);

        var wake = await cli.RunWithInput("""{"text":"ok","senderKind":"teammate"}""", "wake-gate");
        var scope = await cli.RunWithInput("""{"agentType":"Explore","goal":"","acceptanceCriteria":"","proofCommand":""}""", "brief-scope");
        var ask = await cli.RunWithInput("""{"transcriptTail":"Tests pass. Next I'll merge the PR."}""", "ask-check");
        var escalated = await cli.RunWithInput("""{"text":"progress on the parser, half done","senderKind":"teammate"}""", "wake-gate");

        Assert.Equal((0, "drop", ""), (wake.ExitCode, wake.Json().GetProperty("decision").GetString(), wake.Stderr));
        Assert.Equal((0, "ready", ""), (scope.ExitCode, scope.Json().GetProperty("decision").GetString(), scope.Stderr));
        Assert.Equal((0, "ask", ""), (ask.ExitCode, ask.Json().GetProperty("decision").GetString(), ask.Stderr));
        Assert.Equal((2, "no-provider-decided", ""), (escalated.ExitCode, escalated.Json().GetProperty("reasonCode").GetString(), escalated.Stderr));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData(Storage.ReadOnlyDirectory)]
    [InlineData(Storage.CorruptFile)]
    public async Task Health_exits_1_with_one_stderr_line(Storage storage)
    {
        var cli = await Broken(storage);

        OneErrorLine(await cli.Run("health"));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData(Storage.ReadOnlyDirectory)]
    [InlineData(Storage.CorruptFile)]
    public async Task The_brief_commands_exit_1_with_one_stderr_line_that_isnt_no_brief(Storage storage)
    {
        var cli = await Broken(storage);

        var get = await cli.Run("brief", "get", "toolu_9");
        OneErrorLine(get);
        Assert.False(get.Stderr.StartsWith("no brief for", StringComparison.Ordinal));
        OneErrorLine(await cli.Run("brief", "map-by-name", "Explore"));
        OneErrorLine(await cli.RunWithInput(Brief, "brief", "save"));
        OneErrorLine(await cli.Run("brief", "set-agent-id", "toolu_9", "agent-1"));
    }

    // A database that opens read-only still answers what it holds; only the writes fail.
    [Fact, Trait("Lane", "integration")]
    public async Task A_read_only_database_still_answers_reads_and_refuses_writes_with_one_line()
    {
        var cli = await Broken(Storage.ReadOnlyDatabase);

        var health = await cli.Run("health");
        var get = await cli.Run("brief", "get", "toolu_9");

        Assert.Equal((0, "ok"), (health.ExitCode, health.Json().GetProperty("status").GetString()));
        Assert.Equal((1, "no brief for tool_use_id: toolu_9\n"), (get.ExitCode, get.Stderr));
        OneErrorLine(await cli.RunWithInput(Brief, "brief", "save"));
        OneErrorLine(await cli.Run("brief", "set-agent-id", "toolu_9", "agent-1"));
    }

    public void Dispose()
    {
        if (Directory.Exists(JudgeDirectory))
        {
            Mode(JudgeDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        _state.Dispose();
    }
}
