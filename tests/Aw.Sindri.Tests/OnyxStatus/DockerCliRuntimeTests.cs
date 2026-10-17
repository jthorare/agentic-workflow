using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxStatus;

// The adapter over a scripted tool runner: the commands it runs and how it reads what they print.
public sealed class DockerCliRuntimeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ToolRun Ok(string output) => new(ToolOutcome.Exited, 0, output + "\n");

    private static ToolRun Fails { get; } = new(ToolOutcome.Exited, 1, "");

    private static FakeToolRunner Docker(ToolRun version, ToolRun compose, ToolRun info) => new((_, arguments) => arguments[0] switch
    {
        "version" => version,
        "compose" => compose,
        _ => info,
    });

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reports_missing_cli()
    {
        var facts = new DockerCliRuntime(new FakeToolRunner(ToolRun.Missing)).Inspect(Token);

        Assert.False(facts.CliFound);
        Assert.False(facts.DaemonAnswers);
        Assert.False(facts.ComposeV2);
        Assert.Null(facts.MemoryBytes);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reports_daemon_down_when_server_version_fails()
    {
        // Docker installed, Docker Desktop not started: `docker version` prints the client and fails on the server.
        var facts = new DockerCliRuntime(Docker(Fails, Ok("v2.40.3"), Fails)).Inspect(Token);

        Assert.True(facts.CliFound);
        Assert.False(facts.DaemonAnswers);
        Assert.Null(facts.ServerVersion);
        Assert.True(facts.ComposeV2);
        Assert.Null(facts.MemoryBytes);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("permission denied while trying to connect to the Docker daemon socket at unix:///var/run/docker.sock: Get \"http://%2Fvar%2Frun%2Fdocker.sock/v1.51/version\": dial unix /var/run/docker.sock: connect: permission denied", DaemonProblem.PermissionDenied)]
    [InlineData("Failed to initialize: unable to resolve docker endpoint: context \"nope\": context not found: open /home/u/.docker/contexts/meta/x/meta.json: no such file or directory", DaemonProblem.ContextMissing)]
    [InlineData("Current context \"colima\" does not exist", DaemonProblem.ContextMissing)]
    [InlineData("failed to connect to the docker API at unix:///Users/u/.docker/run/docker.sock; check if the path is correct and if the daemon is running: dial unix /Users/u/.docker/run/docker.sock: connect: no such file or directory", DaemonProblem.NotRunning)]
    [InlineData("", DaemonProblem.NotRunning)]
    public void Inspect_classifies_a_failed_version_by_its_stderr(string stderr, DaemonProblem expected)
    {
        var facts = new DockerCliRuntime(Docker(new ToolRun(ToolOutcome.Exited, 1, "Client: ...", stderr), Ok("v2.40.3"), Fails)).Inspect(Token);

        Assert.False(facts.DaemonAnswers);
        Assert.Equal(expected, facts.DaemonProblem);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reports_no_problem_when_the_daemon_answers()
        => Assert.Equal(DaemonProblem.None, new DockerCliRuntime(DockerWithInfo("29.4.0", "16818978816", "/var/lib/docker")).Inspect(Token).DaemonProblem);

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reports_a_daemon_that_times_out_twice_as_starting_after_25_seconds()
    {
        // A daemon that is waking up and hangs: the 5 s try and the 20 s retry both end in the timeout.
        var runner = Docker(ToolRun.TimedOut, ToolRun.TimedOut, ToolRun.TimedOut);

        var facts = new DockerCliRuntime(runner).Inspect(Token);

        Assert.True(facts.CliFound);
        Assert.False(facts.DaemonAnswers);
        Assert.False(facts.ComposeV2);
        Assert.Equal(DaemonProblem.Starting, facts.DaemonProblem);
        Assert.Equal(25, facts.WaitedSeconds);
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20)], runner.Calls.Where(static c => c.Arguments[0] == "version").Select(static c => c.Timeout));
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_retries_a_timed_out_version_once_with_a_longer_budget_and_succeeds()
    {
        var versionCalls = 0;
        var runner = new FakeToolRunner((_, arguments) => arguments[0] switch
        {
            "version" => ++versionCalls == 1 ? ToolRun.TimedOut : Ok("29.4.0"),
            "compose" => Ok("v2.40.3"),
            _ => Ok("16818978816|Docker Desktop|/var/lib/docker"),
        });

        var facts = new DockerCliRuntime(runner).Inspect(Token);

        Assert.True(facts.DaemonAnswers);
        Assert.Equal("29.4.0", facts.ServerVersion);
        Assert.Equal(DaemonProblem.None, facts.DaemonProblem);
        Assert.Null(facts.WaitedSeconds);
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20)], runner.Calls.Where(static c => c.Arguments[0] == "version").Select(static c => c.Timeout));
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_does_not_retry_a_version_that_failed_without_timing_out()
    {
        var runner = Docker(new ToolRun(ToolOutcome.Exited, 1, "", "permission denied"), Ok("v2.40.3"), Fails);

        var facts = new DockerCliRuntime(runner).Inspect(Token);

        _ = Assert.Single(runner.Calls, static c => c.Arguments[0] == "version");
        Assert.Equal(DaemonProblem.PermissionDenied, facts.DaemonProblem);
        Assert.Null(facts.WaitedSeconds);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reports_compose_v1_only()
    {
        // Only legacy docker-compose (v1): `docker compose version` fails.
        var facts = new DockerCliRuntime(Docker(Ok("29.4.0"), Fails, Ok("16818978816|Docker Desktop|/var/lib/docker"))).Inspect(Token);

        Assert.True(facts.DaemonAnswers);
        Assert.False(facts.ComposeV2);
        Assert.Null(facts.ComposeVersion);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reads_memory_bytes()
    {
        var facts = new DockerCliRuntime(Docker(Ok("29.4.0"), Ok("v2.40.3"), Ok("16818978816|Docker Desktop|/var/lib/docker"))).Inspect(Token);

        Assert.Equal(16_818_978_816, facts.MemoryBytes);
        Assert.Equal("29.4.0", facts.ServerVersion);
        Assert.Equal("v2.40.3", facts.ComposeVersion);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reports_a_version_that_is_not_plain_text_as_unreadable_with_the_fact_still_true()
    {
        var facts = new DockerCliRuntime(Docker(Ok("29.4.0\u001b[2J\u001b]0;pwned\a"), Ok("v2.40.3 \u001b[31m"), Ok("16818978816|Docker Desktop|/var/lib/docker"))).Inspect(Token);

        Assert.True(facts.DaemonAnswers);
        Assert.Null(facts.ServerVersion);
        Assert.True(facts.ComposeV2);
        Assert.Null(facts.ComposeVersion);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reads_the_data_root()
    {
        var facts = new DockerCliRuntime(DockerWithInfo("29.4.0", "16818978816", "/var/lib/docker")).Inspect(Token);

        Assert.Equal("/var/lib/docker", facts.DataRoot);
        Assert.Equal(16_818_978_816, facts.MemoryBytes);
    }

    private static FakeToolRunner DockerWithInfo(string version, string memory, string dataRoot, string operatingSystem = "Docker Desktop") => new((_, arguments) => arguments[0] switch
    {
        "version" => Ok(version),
        "compose" => Ok("v2.40.3"),
        _ => Ok(memory + "|" + operatingSystem + "|" + dataRoot),
    });

    [Fact, Trait("Lane", "unit")]
    public void Inspect_runs_exactly_the_three_commands_with_a_five_second_timeout_each()
    {
        var runner = DockerWithInfo("29.4.0", "16818978816", "/var/lib/docker");

        _ = new DockerCliRuntime(runner).Inspect(Token);

        Assert.Equal(
            [
                ["version", "--format", "{{.Server.Version}}"],
                ["compose", "version", "--short"],
                ["info", "--format", "{{.MemTotal}}|{{.OperatingSystem}}|{{.DockerRootDir}}"],
            ],
            runner.Calls.Select(static c => c.Arguments));
        Assert.All(runner.Calls, static c => Assert.Equal("docker", c.FileName));
        Assert.All(runner.Calls, static c => Assert.Equal(TimeSpan.FromSeconds(5), c.Timeout));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("relative/docker")]
    [InlineData("/var/lib/\u001b[2Jdocker")]
    [InlineData("/var/lib/dock\ner")]
    [InlineData("")]
    public void Inspect_reads_a_relative_or_control_character_data_root_as_unknown_and_keeps_the_memory(string dataRoot)
    {
        var facts = new DockerCliRuntime(DockerWithInfo("29.4.0", "16818978816", dataRoot)).Inspect(Token);

        Assert.Null(facts.DataRoot);
        Assert.Equal(16_818_978_816, facts.MemoryBytes);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("lots|OrbStack|/var/lib/docker", null, "OrbStack", "/var/lib/docker")]
    [InlineData("16818978816|OrbStack", null, null, null)]
    [InlineData("16818978816|OrbStack|/srv/a|b", 16_818_978_816, "OrbStack", "/srv/a|b")]
    [InlineData("16818978816||/var/lib/docker", 16_818_978_816, null, "/var/lib/docker")]
    [InlineData("16818978816|Dock\u001b[2Jer|/var/lib/docker", 16_818_978_816, null, "/var/lib/docker")]
    public void Inspect_guards_each_part_of_the_info_line_on_its_own(string info, long? memory, string? operatingSystem, string? dataRoot)
    {
        var facts = new DockerCliRuntime(Docker(Ok("29.4.0"), Ok("v2.40.3"), Ok(info))).Inspect(Token);

        Assert.Equal(memory, facts.MemoryBytes);
        Assert.Equal(operatingSystem, facts.OperatingSystem);
        Assert.Equal(dataRoot, facts.DataRoot);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reads_the_operating_system_of_this_boxs_runtime()
    {
        // `docker info --format '{{.OperatingSystem}}'` on an OrbStack box prints exactly "OrbStack".
        var facts = new DockerCliRuntime(DockerWithInfo("29.4.0", "16818978816", "/var/lib/docker", "OrbStack")).Inspect(Token);

        Assert.Equal("OrbStack", facts.OperatingSystem);
        Assert.True(facts.DataInVm);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_does_not_ask_info_of_a_daemon_that_does_not_answer()
    {
        var runner = Docker(Fails, Ok("v2.40.3"), Ok("16818978816|Docker Desktop|/var/lib/docker"));

        _ = new DockerCliRuntime(runner).Inspect(Token);

        Assert.DoesNotContain(runner.Calls, static c => c.Arguments[0] == "info");
    }
}
