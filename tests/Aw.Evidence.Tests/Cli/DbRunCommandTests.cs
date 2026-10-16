using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Aw.Evidence.Core.Locks;
using Aw.Evidence.Host;
using Aw.Evidence.Host.Commands;
using Aw.Evidence.Testing;
using Aw.Evidence.Tests.Infrastructure;
using Aw.Kernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Evidence.Tests.Cli;

/// <summary>The db-run command end to end in process: the real composition, command table, run files, locator, SQLite reader and scrubber, with only the locks faked.</summary>
[Lane(TestLane.Integration)]
[Collection(ConsoleCapture.Name)]
public sealed class DbRunCommandTests : IDisposable
{
    private readonly TempDirectory _root = new();
    private readonly InMemoryMachineLocks _locks = new();

    private string Repo => _root.Combine("repo");

    private string Source => _root.Combine("repo/data/notes.db");

    private string RunDir => _root.Combine("runs/db-1");

    private string SummaryFile => Path.Combine(RunDir, "summary.json");

    private async Task<string> Script(string queries, string extra = "")
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(Source)!);
        if (!File.Exists(Source))
        {
            await SqliteDatabaseReaderContractTests.MakeNotesAsync(Source);
        }

        var path = _root.Combine("db.json");
        await File.WriteAllTextAsync(path, $$"""{ {{extra}} "sqlite":"data/notes.db","queries":[{{queries}}]}""", TestContext.Current.CancellationToken);
        return path;
    }

    private static string Query(string sql, string expect) => $$"""{"sql":"{{sql}}","expect":{{expect}}}""";

    private async Task<(int Exit, string Out, string Err)> Run(string script, params string[] extra)
    {
        var services = Composition.Compose(
            new ServiceCollection(), name => name switch { "AW_STATE_DIR" => _root.Combine("state"), "AW_LOG_LEVEL" => "none", _ => null }, new FakeTimeProvider());
        _ = services.Replace(ServiceDescriptor.Singleton<IMachineLocks>(_locks));
        await using var provider = services.BuildServiceProvider();
        var (output, error) = (new StringWriter(), new StringWriter());
        var (stdout, stderr) = (Console.Out, Console.Error);
        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            string[] args = ["db-run", script, RunDir, "--repo", Repo, .. extra];
            var exit = await CommandTable.Run(args, provider);
            return (exit, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
        }
    }

    private static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private List<string> Files() => [.. Directory.EnumerateFiles(_root.Path, "*", SearchOption.AllDirectories).Where(static f => !f.EndsWith("-wal", StringComparison.Ordinal) && !f.EndsWith("-shm", StringComparison.Ordinal))];

    [Fact]
    public async Task DbRunCommand_exits_0_and_prints_exactly_the_summary_file_for_rows_that_hold()
    {
        var script = await Script(Query("SELECT text FROM notes ORDER BY id", """{"rowCount":3,"pointers":[{"pointer":"/0/text","equals":"hello"}]}"""));
        var before = Sha(Source);

        var (exit, output, error) = await Run(script, "--at", "head");

        Assert.Equal(0, exit);
        Assert.Equal(await File.ReadAllTextAsync(SummaryFile, TestContext.Current.CancellationToken), output);
        Assert.Equal("passed", JsonNode.Parse(output)!["outcome"]!.GetValue<string>());
        Assert.Equal(string.Empty, error);
        Assert.Equal(before, Sha(Source));
    }

    [Fact]
    public async Task DbRunCommand_exits_2_when_an_expectation_did_not_hold()
    {
        var script = await Script(Query("SELECT text FROM notes", """{"rowCount":9}"""));

        var (exit, output, _) = await Run(script, "--at", "base");

        Assert.Equal(2, exit);
        Assert.Equal("failed", JsonNode.Parse(output)!["outcome"]!.GetValue<string>());
        Assert.Equal("base", JsonNode.Parse(output)!["at"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("INSERT INTO notes(text) VALUES ('w')")]
    [InlineData("WITH x AS (SELECT 1) INSERT INTO notes(text) SELECT 'w' FROM x")]
    [InlineData("PRAGMA user_version = 9")]
    [InlineData("ATTACH 'new.db' AS n")]
    [InlineData("VACUUM INTO 'copy.db'")]
    public async Task DbRunCommand_exits_1_with_AW_EVIDENCE_020_for_each_bypass_and_changes_nothing(string sql)
    {
        var script = await Script(Query(sql, """{"rowCount":1}"""));
        var before = Sha(Source);
        var files = Files();

        var (exit, output, error) = await Run(script, "--at", "head");

        Assert.Equal(1, exit);
        Assert.StartsWith("ui-evidence: AW-EVIDENCE-020", error, StringComparison.Ordinal);
        Assert.Equal("refused", JsonNode.Parse(output)!["outcome"]!.GetValue<string>());
        Assert.Equal(before, Sha(Source));
        Assert.Equal(files.Count + 1, Files().Count);   // only the summary is new
    }

    [Fact]
    public async Task DbRunCommand_checks_a_golden_file_against_real_sqlite_rows()
    {
        var script = await Script(Query("SELECT id, text FROM notes ORDER BY id LIMIT 1", """{"golden":"gold.json"}"""));
        await File.WriteAllTextAsync(_root.Combine("gold.json"), """[{"id":1,"text":"hello"}]""", TestContext.Current.CancellationToken);
        var (passedExit, _, _) = await Run(script, "--at", "head");
        await File.WriteAllTextAsync(_root.Combine("gold.json"), """[{"id":"1","text":"hello"}]""", TestContext.Current.CancellationToken);

        var (failedExit, output, _) = await Run(script, "--at", "head");

        Assert.Equal((0, 2), (passedExit, failedExit));
        Assert.Equal("failed", JsonNode.Parse(output)!["outcome"]!.GetValue<string>());
    }

    [Fact]
    public async Task DbRunCommand_exits_1_with_AW_EVIDENCE_018_when_the_seed_fails_on_the_copy()
    {
        var script = await Script(Query("SELECT 1", """{"rowCount":1}"""), "\"seed\":\"seed.sql\",");
        await File.WriteAllTextAsync(_root.Combine("seed.sql"), "INSERT INTO no_such_table VALUES (1)", TestContext.Current.CancellationToken);
        var before = Sha(Source);

        var (exit, _, error) = await Run(script, "--at", "head");

        Assert.Equal(1, exit);
        Assert.StartsWith("ui-evidence: AW-EVIDENCE-018", error, StringComparison.Ordinal);
        Assert.Equal(before, Sha(Source));
    }

    [Fact]
    public async Task DbRunCommand_seeds_the_run_copy_and_never_the_source()
    {
        var script = await Script(Query("SELECT count(*) AS n FROM notes", """{"pointers":[{"pointer":"/0/n","equals":4}]}"""), "\"seed\":\"seed.sql\",");
        await File.WriteAllTextAsync(_root.Combine("seed.sql"), "INSERT INTO notes(text) VALUES ('seeded');", TestContext.Current.CancellationToken);
        var before = Sha(Source);

        var (exit, output, _) = await Run(script, "--at", "head");

        var json = JsonNode.Parse(output)!;
        Assert.Equal(0, exit);
        Assert.Equal("seeded", json["provenance"]!.GetValue<string>());
        Assert.Equal(Sha(_root.Combine("seed.sql")), json["seedSha256"]!.GetValue<string>().ToUpperInvariant());
        Assert.Equal(before, Sha(Source));
    }

    [Fact]
    public async Task DbRunCommand_exits_1_for_a_sqlite_file_that_a_link_leads_out_of_the_repo()
    {
        var script = await Script(Query("SELECT 1", """{"rowCount":1}"""));
        var outside = _root.Combine("outside/other.db");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(outside)!);
        File.Move(Source, outside);
        _ = File.CreateSymbolicLink(Source, outside);

        var (exit, _, error) = await Run(script, "--at", "head");

        Assert.Equal(1, exit);
        Assert.StartsWith("ui-evidence: AW-EVIDENCE-020", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DbRunCommand_exits_75_with_no_summary_when_a_lock_stays_busy()
    {
        var script = await Script(Query("SELECT 1", """{"rowCount":1}"""));
        _ = _locks.AlwaysBusy.Add(LockKind.QaStack);

        var (exit, output, _) = await Run(script, "--at", "head");

        Assert.Equal(75, exit);
        Assert.Equal(string.Empty, output);
        Assert.False(File.Exists(SummaryFile));
    }

    [Fact]
    public async Task DbRunCommand_a_sqlite_script_with_a_missing_config_exits_1_with_AW_EVIDENCE_010()
    {
        var script = await Script(Query("SELECT 1", """{"rowCount":1}"""));

        var (exit, _, error) = await Run(script, "--at", "head", "--config", _root.Combine("nope.json"));

        Assert.Equal(1, exit);
        Assert.StartsWith("ui-evidence: AW-EVIDENCE-010", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DbRunCommand_a_sqlite_script_with_an_invalid_config_exits_1_with_022(bool throughConfigFlag)
    {
        var script = await Script(Query("SELECT 1", """{"rowCount":1}"""));
        var broken = throughConfigFlag ? _root.Combine("broken.json") : Path.Combine(Repo, ".agentic-workflow", "evidence.json");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(broken)!);
        await File.WriteAllTextAsync(broken, "{ not json", TestContext.Current.CancellationToken);

        var (exit, _, error) = await Run(script, throughConfigFlag ? ["--at", "head", "--config", broken] : ["--at", "head"]);

        Assert.Equal(1, exit);
        Assert.StartsWith("ui-evidence: AW-EVIDENCE-022", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SELECT api_key AS copy_of_key FROM accounts")]
    [InlineData("SELECT name, api_key, 'key is ' || api_key AS sentence FROM accounts")]
    public async Task DbRunCommand_a_secret_named_column_s_value_is_not_recorded_under_another_name(string sql)
    {
        var script = await Script(Query(sql, """{"rowCount":1}"""));
        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Source}"))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE accounts(name TEXT, api_key TEXT); INSERT INTO accounts VALUES ('ann', 'ak-PLANTED-col-123456')";
            _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var (exit, output, _) = await Run(script, "--at", "head");

        Assert.Equal(0, exit);
        Assert.DoesNotContain("ak-PLANTED-col-123456", output, StringComparison.Ordinal);
        Assert.DoesNotContain("ak-PLANTED-col-123456", await File.ReadAllTextAsync(SummaryFile, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData]
    [InlineData("--at", "sideways")]
    [InlineData("--at", "head", "--host", "http://localhost:3000")]
    public async Task DbRunCommand_exits_1_with_the_usage_line_for_bad_arguments(params string[] extra)
    {
        var script = await Script(Query("SELECT 1", """{"rowCount":1}"""));

        var (exit, _, error) = await Run(script, extra);

        Assert.Equal(1, exit);
        Assert.StartsWith("ui-evidence: usage: ui-evidence db-run ", error, StringComparison.Ordinal);
    }

    public void Dispose() => _root.Dispose();
}
