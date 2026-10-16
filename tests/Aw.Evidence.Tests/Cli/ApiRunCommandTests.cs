using System.Text.Json.Nodes;
using Aw.Evidence.Core.Data;
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

/// <summary>The api-run command end to end in process: the real composition, command table, run files, targets and scrubber, with the transport and locks swapped for fakes.</summary>
[Lane(TestLane.Integration)]
[Collection(ConsoleCapture.Name)]
public sealed class ApiRunCommandTests : IDisposable
{
    private const string Origin = "http://localhost:3000";
    private readonly TempDirectory _root = new();
    private readonly FakeApiTransport _transport = new();
    private readonly InMemoryMachineLocks _locks = new();

    private string RunDir => _root.Combine("runs/api-1");

    private string SummaryFile => Path.Combine(RunDir, "summary.json");

    private async Task<string> Script(string json)
    {
        var path = _root.Combine("api.json");
        await File.WriteAllTextAsync(path, json, TestContext.Current.CancellationToken);
        return path;
    }

    private async Task<(int Exit, string Out, string Err)> Run(string script, params string[] extra)
    {
        var services = Composition.Compose(
            new ServiceCollection(), name => name switch { "AW_STATE_DIR" => _root.Combine("state"), "AW_LOG_LEVEL" => "none", _ => null }, new FakeTimeProvider());
        _ = services.Replace(ServiceDescriptor.Singleton<IApiTransport>(_transport));
        _ = services.Replace(ServiceDescriptor.Singleton<IMachineLocks>(_locks));
        await using var provider = services.BuildServiceProvider();
        var (output, error) = (new StringWriter(), new StringWriter());
        var (stdout, stderr) = (Console.Out, Console.Error);
        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            string[] args = ["api-run", script, RunDir, .. extra];
            var exit = await CommandTable.Run(args, provider);
            return (exit, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
        }
    }

    private static string Get(string expect) =>
        """{"requests":[{"method":"GET","path":"/api/notes","expect":EXPECT}]}""".Replace("EXPECT", expect, StringComparison.Ordinal);

    private ApiRunCommandTests Notes(int status = 200)
    {
        _ = _transport.Reply("GET", Origin + "/api/notes", new HttpReply(status, Location: null, """{"n":1}"""));
        return this;
    }

    [Fact]
    public async Task ApiRunCommand_exits_0_when_every_expectation_holds_and_prints_exactly_the_summary_file()
    {
        _ = Notes();

        var (exit, output, error) = await Run(await Script(Get("""{"status":200,"pointers":[{"pointer":"/n","equals":1}]}""")), "--at", "head", "--host", Origin);

        Assert.Equal(0, exit);
        Assert.Equal(await File.ReadAllTextAsync(SummaryFile, TestContext.Current.CancellationToken), output);
        Assert.Equal("passed", JsonNode.Parse(output)!["outcome"]!.GetValue<string>());
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public async Task ApiRunCommand_exits_1_with_AW_EVIDENCE_022_for_a_targets_file_with_a_malformed_databases_entry()
    {
        var config = _root.Combine("targets.json");
        await File.WriteAllTextAsync(config, """{"databases":{"fixture":{"engine":"mysql"}}}""", TestContext.Current.CancellationToken);

        var (exit, _, error) = await Run(await Script(Get("""{"status":200}""")), "--at", "head", "--host", Origin, "--config", config);

        Assert.Equal(1, exit);
        Assert.StartsWith("ui-evidence: AW-EVIDENCE-022 ", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApiRunCommand_exits_2_when_an_expectation_did_not_hold()
    {
        _ = Notes();

        var (exit, output, _) = await Run(await Script(Get("""{"status":201}""")), "--at", "base", "--host", Origin);

        Assert.Equal(2, exit);
        Assert.Equal("failed", JsonNode.Parse(output)!["outcome"]!.GetValue<string>());
        Assert.Equal("base", JsonNode.Parse(output)!["at"]!.GetValue<string>());
    }

    [Fact]
    public async Task ApiRunCommand_exits_1_with_the_code_on_stderr_and_a_refusal_in_the_summary()
    {
        var (exit, output, error) = await Run(await Script(Get("""{"status":200}""")), "--at", "head");

        Assert.Equal(1, exit);
        Assert.StartsWith("ui-evidence: AW-EVIDENCE-004 ", error, StringComparison.Ordinal);
        var summary = JsonNode.Parse(output)!;
        Assert.Equal("refused", summary["outcome"]!.GetValue<string>());
        Assert.Equal("AW-EVIDENCE-004", summary["refusal"]!["code"]!.GetValue<string>());
        Assert.Equal(output, await File.ReadAllTextAsync(SummaryFile, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ApiRunCommand_exits_4_when_a_request_failed()
    {
        _ = _transport.Unreachable(Origin + "/api/notes");

        var (exit, output, error) = await Run(await Script(Get("""{"status":200}""")), "--at", "head", "--host", Origin);

        Assert.Equal(4, exit);
        Assert.Equal("errored", JsonNode.Parse(output)!["outcome"]!.GetValue<string>());
        Assert.StartsWith("ui-evidence: AW-EVIDENCE-023 ", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApiRunCommand_exits_75_with_no_summary_when_a_lock_stays_busy()
    {
        _ = Notes();
        _ = Directory.CreateDirectory(RunDir);
        await File.WriteAllTextAsync(SummaryFile, "{}", TestContext.Current.CancellationToken);
        _ = _locks.AlwaysBusy.Add(LockKind.HeavyJob);

        var (exit, output, error) = await Run(await Script(Get("""{"status":200}""")), "--at", "head", "--host", Origin, "--lock-timeout", "0");

        Assert.Equal((75, string.Empty), (exit, output));
        Assert.StartsWith("ui-evidence: AW-EVIDENCE-009 ", error, StringComparison.Ordinal);
        Assert.False(File.Exists(SummaryFile));
    }

    [Theory]
    [InlineData("passed", 0)]
    [InlineData("failed", 2)]
    [InlineData("refused", 1)]
    [InlineData("errored", 4)]
    public async Task The_exit_code_and_the_summary_outcome_agree(string outcome, int exitCode)
    {
        _ = Notes();
        string[] flags = outcome switch
        {
            "refused" => ["--at", "head"],
            _ => ["--at", "head", "--host", Origin],
        };
        if (outcome == "errored")
        {
            _ = _transport.Unreachable(Origin + "/api/notes");
        }

        var expect = outcome == "failed" ? """{"status":404}""" : """{"status":200}""";
        var (exit, output, _) = await Run(await Script(Get(expect)), flags);

        Assert.Equal((exitCode, outcome), (exit, JsonNode.Parse(output)!["outcome"]!.GetValue<string>()));
    }

    [Theory]
    [InlineData("--host", Origin)]
    [InlineData("--at", "tomorrow", "--host", Origin)]
    [InlineData("--at", "head", "--bogus", "1")]
    public async Task ApiRunCommand_exits_1_with_the_usage_line_for_bad_arguments(params string[] extra)
    {
        var (exit, output, error) = await Run(await Script(Get("""{"status":200}""")), extra);

        Assert.Equal((1, string.Empty), (exit, output));
        Assert.StartsWith("ui-evidence: usage: ui-evidence api-run ", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApiRunCommand_writes_only_inside_the_run_directory()
    {
        _ = Notes();
        var script = await Script(Get("""{"status":200}"""));
        var before = Snapshot();

        _ = await Run(script, "--at", "head", "--host", Origin);

        Assert.All(Snapshot().Except(before, StringComparer.Ordinal), file => Assert.StartsWith(RunDir + "/", file, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ApiRunCommand_runs_a_program_from_the_repo_build_output_with_the_real_runner()
    {
        var repo = _root.Combine("repo");
        _ = Directory.CreateDirectory(Path.Combine(repo, ".agentic-workflow"));
        _ = Directory.CreateDirectory(Path.Combine(repo, "out"));
        await File.WriteAllTextAsync(Path.Combine(repo, ".agentic-workflow", "evidence.json"), """{"schema":"evidence.repo/1","cli":{"buildOutputs":["out"]}}""", TestContext.Current.CancellationToken);
        _ = StubCommand.Write(_root, "repo/out/tool", "printf '{\"count\":2}'");
        var script = await Script("""{"cli":[{"argv":["out/tool","list"],"expect":{"exitCode":0,"pointers":[{"pointer":"/count","equals":2}]}}]}""");

        var (exit, output, _) = await Run(script, "--at", "head", "--repo", repo);

        Assert.Equal(0, exit);
        Assert.Equal("out/tool list", JsonNode.Parse(output)!["steps"]![0]!["target"]!.GetValue<string>());
    }

    public void Dispose() => _root.Dispose();

    private List<string> Snapshot() => [.. Directory.EnumerateFiles(_root.Path, "*", SearchOption.AllDirectories)];

    [Fact]
    public async Task ApiRunCommand_records_no_short_value_of_a_secret_named_member_anywhere_in_the_summary()
    {
        _ = _transport.Reply("GET", Origin + "/api/notes", new HttpReply(200, Location: null, """{"password":"s3cr3t-Pw","nested":{"clientSecret":"abc","list":[{"apiKey":"k9"}]},"n":1}"""));

        var (exit, output, _) = await Run(
            await Script(Get("""{"status":200,"pointers":[{"pointer":"/password","equals":"wrong"},{"pointer":"/nested/list/0/apiKey","equals":"wrong"}]}""")), "--at", "head", "--host", Origin);

        Assert.Equal(2, exit);
        foreach (var value in new[] { "s3cr3t-Pw", "\"abc\"", "k9" })
        {
            Assert.DoesNotContain(value, output, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("s3cr3t-Pw", await File.ReadAllTextAsync(SummaryFile, TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Contains("\"n\":1", JsonNode.Parse(output)!["steps"]![0]!["output"]!.GetValue<string>(), StringComparison.Ordinal);
    }
}
