using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxStatus;

// The adapter over a scripted tool runner: the commands it runs and how it reads what they print.
public sealed class DockerCliRuntimeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ToolRun Ok(string output) => new(ToolOutcome.Exited, 0, output + "\n");

    private static ToolRun Fails { get; } = new(ToolOutcome.Exited, 1, "");

    private static DockerCliRuntime Cli(IToolRunner runner, Func<string, string?>? environment = null) => new(runner, environment ?? (static _ => null));

    private static FakeToolRunner Docker(ToolRun version, ToolRun compose, ToolRun info) => new((_, arguments) => arguments[0] switch
    {
        "version" => version,
        "compose" => compose,
        _ => info,
    });

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reports_missing_cli()
    {
        var facts = Cli(new FakeToolRunner(ToolRun.Missing)).Inspect(Token);

        Assert.False(facts.CliFound);
        Assert.False(facts.DaemonAnswers);
        Assert.False(facts.ComposeV2);
        Assert.Null(facts.MemoryBytes);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reports_daemon_down_when_server_version_fails()
    {
        // Docker installed, Docker Desktop not started: `docker version` prints the client and fails on the server.
        var facts = Cli(Docker(Fails, Ok("v2.40.3"), Fails)).Inspect(Token);

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
        var facts = Cli(Docker(new ToolRun(ToolOutcome.Exited, 1, "Client: ...", stderr), Ok("v2.40.3"), Fails)).Inspect(Token);

        Assert.False(facts.DaemonAnswers);
        Assert.Equal(expected, facts.DaemonProblem);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reports_no_problem_when_the_daemon_answers()
        => Assert.Equal(DaemonProblem.None, Cli(DockerWithInfo("29.4.0", "16818978816", "/var/lib/docker")).Inspect(Token).DaemonProblem);

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reports_a_daemon_that_times_out_twice_as_starting_after_25_seconds()
    {
        // A daemon that is waking up and hangs: the 5 s try and the 20 s retry both end in the timeout.
        var runner = Docker(ToolRun.TimedOut, ToolRun.TimedOut, ToolRun.TimedOut);

        var facts = Cli(runner).Inspect(Token);

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

        var facts = Cli(runner).Inspect(Token);

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

        var facts = Cli(runner).Inspect(Token);

        _ = Assert.Single(runner.Calls, static c => c.Arguments[0] == "version");
        Assert.Equal(DaemonProblem.PermissionDenied, facts.DaemonProblem);
        Assert.Null(facts.WaitedSeconds);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reports_compose_v1_only()
    {
        // Only legacy docker-compose (v1): `docker compose version` fails.
        var facts = Cli(Docker(Ok("29.4.0"), Fails, Ok("16818978816|Docker Desktop|/var/lib/docker"))).Inspect(Token);

        Assert.True(facts.DaemonAnswers);
        Assert.False(facts.ComposeV2);
        Assert.Null(facts.ComposeVersion);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reads_memory_bytes()
    {
        var facts = Cli(Docker(Ok("29.4.0"), Ok("v2.40.3"), Ok("16818978816|Docker Desktop|/var/lib/docker"))).Inspect(Token);

        Assert.Equal(16_818_978_816, facts.MemoryBytes);
        Assert.Equal("29.4.0", facts.ServerVersion);
        Assert.Equal("v2.40.3", facts.ComposeVersion);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reports_a_version_that_is_not_plain_text_as_unreadable_with_the_fact_still_true()
    {
        var facts = Cli(Docker(Ok("29.4.0\u001b[2J\u001b]0;pwned\a"), Ok("v2.40.3 \u001b[31m"), Ok("16818978816|Docker Desktop|/var/lib/docker"))).Inspect(Token);

        Assert.True(facts.DaemonAnswers);
        Assert.Null(facts.ServerVersion);
        Assert.True(facts.ComposeV2);
        Assert.Null(facts.ComposeVersion);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reads_the_data_root()
    {
        var facts = Cli(DockerWithInfo("29.4.0", "16818978816", "/var/lib/docker")).Inspect(Token);

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
    public void Inspect_runs_exactly_the_four_commands_with_a_five_second_timeout_each()
    {
        var runner = DockerWithInfo("29.4.0", "16818978816", "/var/lib/docker");

        _ = Cli(runner).Inspect(Token);

        Assert.Equal(
            [
                ["version", "--format", "{{.Server.Version}}"],
                ["compose", "version", "--short"],
                ["info", "--format", "{{.MemTotal}}|{{.OperatingSystem}}|{{.DockerRootDir}}"],
                ["context", "inspect", "--format", "{{.Endpoints.docker.Host}}"],
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
        var facts = Cli(DockerWithInfo("29.4.0", "16818978816", dataRoot)).Inspect(Token);

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
        var facts = Cli(Docker(Ok("29.4.0"), Ok("v2.40.3"), Ok(info))).Inspect(Token);

        Assert.Equal(memory, facts.MemoryBytes);
        Assert.Equal(operatingSystem, facts.OperatingSystem);
        Assert.Equal(dataRoot, facts.DataRoot);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reads_the_operating_system_of_this_boxs_runtime()
    {
        // `docker info --format '{{.OperatingSystem}}'` on an OrbStack box prints exactly "OrbStack".
        var facts = Cli(DockerWithInfo("29.4.0", "16818978816", "/var/lib/docker", "OrbStack")).Inspect(Token);

        Assert.Equal("OrbStack", facts.OperatingSystem);
        Assert.True(facts.DataInVm);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_does_not_ask_info_of_a_daemon_that_does_not_answer()
    {
        var runner = Docker(Fails, Ok("v2.40.3"), Ok("16818978816|Docker Desktop|/var/lib/docker"));

        _ = Cli(runner).Inspect(Token);

        Assert.DoesNotContain(runner.Calls, static c => c.Arguments[0] == "info");
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reads_DOCKER_HOST_before_the_context()
    {
        var runner = DockerWithInfo("29.4.0", "16818978816", "/var/lib/docker");

        var facts = Cli(runner, name => name == "DOCKER_HOST" ? "tcp://10.0.0.5:2375" : null).Inspect(Token);

        Assert.Equal("tcp://10.0.0.5:2375", facts.Endpoint);
        Assert.DoesNotContain(runner.Calls, static c => c.Arguments[0] == "context");
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reads_the_context_endpoint_otherwise()
    {
        var runner = new FakeToolRunner((_, arguments) => arguments[0] switch
        {
            "version" => Ok("29.4.0"),
            "compose" => Ok("v2.40.3"),
            "context" => Ok("unix:///Users/me/.docker/run/docker.sock"),
            _ => Ok("16818978816|/var/lib/docker"),
        });

        var facts = Cli(runner).Inspect(Token);

        Assert.Equal("unix:///Users/me/.docker/run/docker.sock", facts.Endpoint);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("unix:///var/run/\u001b[2Jdocker.sock")]
    [InlineData("two words")]
    public void Inspect_reads_an_endpoint_that_is_not_a_plain_token_as_unknown(string endpoint)
    {
        var runner = new FakeToolRunner((_, arguments) => arguments[0] switch
        {
            "version" => Ok("29.4.0"),
            "compose" => Ok("v2.40.3"),
            "context" => Ok(endpoint),
            _ => Ok("16818978816|/var/lib/docker"),
        });

        Assert.Null(Cli(runner).Inspect(Token).Endpoint);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reports_no_endpoint_when_the_cli_is_missing()
        => Assert.Null(Cli(new FakeToolRunner(ToolRun.Missing), static _ => "tcp://10.0.0.5:2375").Inspect(Token).Endpoint);

    private static readonly ComposeProject _project = new("sindri-onyx-0a1b2c3d", "/o/docker-compose.yml", "/o/docker-compose.override.yml", "/o/.env");

    private static readonly string[] _composePrefix = ["compose", "-p", "sindri-onyx-0a1b2c3d", "-f", "/o/docker-compose.yml", "-f", "/o/docker-compose.override.yml", "--env-file", "/o/.env"];

    private static ToolRun Exit(int code, string output = "") => new(ToolOutcome.Exited, code, output);

    [Theory, Trait("Lane", "unit")]
    [InlineData("linux/aarch64", "linux/arm64")]
    [InlineData("linux/x86_64", "linux/amd64")]
    [InlineData("linux/arm64", "linux/arm64")]
    [InlineData("linux/amd64", "linux/amd64")]
    public void ServerPlatform_normalizes_aarch64_and_x86_64(string printed, string expected)
    {
        var runner = new FakeToolRunner(Exit(0, printed + "\n"));

        Assert.Equal(expected, Cli(runner).ServerPlatform(Token).Value);
        Assert.Equal(["version", "--format", "{{.Server.Os}}/{{.Server.Arch}}"], Assert.Single(runner.Calls).Arguments);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("linux")]
    [InlineData("linux/arm64\u001b[2J")]
    public void ServerPlatform_refuses_what_it_cannot_read_as_011(string printed)
        => Assert.Equal("SND-ONYX-011", Cli(new FakeToolRunner(Exit(0, printed))).ServerPlatform(Token).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void Pull_passes_the_exact_argument_list_with_the_45_minute_timeout_and_streams_stderr_to_progress()
    {
        var runner = new FakeToolRunner(Exit(0), ["Pulling api_server", "Pulled api_server"]);
        var progress = new List<string>();

        var result = Cli(runner).Pull(_project, progress.Add, Token);

        Assert.True(result.IsSuccess);
        var call = Assert.Single(runner.Calls);
        Assert.Equal("docker", call.FileName);
        Assert.Equal([.. _composePrefix, "pull"], call.Arguments);
        Assert.Equal(TimeSpan.FromMinutes(45), call.Timeout);
        Assert.True(call.HadStderrCallback);
        Assert.Equal(["Pulling api_server", "Pulled api_server"], progress);
    }

    [Fact, Trait("Lane", "unit")]
    public void Up_uses_no_build_and_a_5_minute_timeout()
    {
        var runner = new FakeToolRunner(Exit(0));

        Assert.True(Cli(runner).Up(_project, static _ => { }, limit: null, Token).IsSuccess);

        var call = Assert.Single(runner.Calls);
        Assert.Equal([.. _composePrefix, "up", "-d", "--no-build"], call.Arguments);
        Assert.Equal(TimeSpan.FromMinutes(5), call.Timeout);
    }

    [Fact, Trait("Lane", "unit")]
    public void Up_never_runs_longer_than_the_time_left_the_caller_passes()
    {
        var runner = new FakeToolRunner(Exit(0));

        _ = Cli(runner).Up(_project, static _ => { }, TimeSpan.FromSeconds(40), Token);
        _ = Cli(runner).Up(_project, static _ => { }, TimeSpan.FromHours(1), Token);

        Assert.Equal([TimeSpan.FromSeconds(40), TimeSpan.FromMinutes(5)], runner.Calls.Select(static c => c.Timeout));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_up_stopped_at_a_short_limit_is_cut_short_011_not_a_hung_docker()
    {
        var runner = new FakeToolRunner(ToolRun.TimedOut);

        var error = Cli(runner).Up(_project, static _ => { }, TimeSpan.FromSeconds(20), Token).Error;

        Assert.Equal("SND-ONYX-011", error.Code);
        Assert.True(ComposeFailures.IsTimeout(error));
    }

    private const string ConfigJson = "{\"name\":\"sindri-onyx\",\"services\":{\"nginx\":{\"image\":\"nginx@sha256:aaa\",\"environment\":{\"USER_AUTH_SECRET\":\"hunter2-secret\"},\"ports\":[{\"mode\":\"ingress\",\"host_ip\":\"127.0.0.1\",\"target\":80,\"published\":\"3000\",\"protocol\":\"tcp\"}]},\"cache\":{\"image\":\"redis\"},\"relational_db\":{\"ports\":[{\"mode\":\"ingress\",\"target\":5432,\"protocol\":\"tcp\"}]}}}";

    [Fact, Trait("Lane", "unit")]
    public void RenderedConfig_reads_compose_config_json_and_names_a_port_with_no_address_or_host_port()
    {
        var runner = new FakeToolRunner(Exit(0, ConfigJson));

        var ports = Cli(runner).RenderedConfig(_project, Token).Value.Ports;

        Assert.Equal([.. _composePrefix, "config", "--format", "json"], Assert.Single(runner.Calls).Arguments);
        Assert.Equal(
            [new ComposePublishedPort("nginx", "127.0.0.1", 3000, 80), new ComposePublishedPort("relational_db", "", 0, 5432)],
            ports);
    }

    [Fact, Trait("Lane", "unit")]
    public void RenderedConfig_reports_each_service_that_has_the_hosts_network_processes_or_privileges()
    {
        const string json = "{\"services\":{\"api_server\":{\"network_mode\":\"host\"},\"background\":{\"privileged\":true},\"web_server\":{\"pid\":\"host\"},"
            + "\"cache\":{\"privileged\":false,\"network_mode\":\"bridge\",\"pid\":\"service:cache\"},\"nginx\":{\"image\":\"nginx\"}}}";

        var access = Cli(new FakeToolRunner(Exit(0, json))).RenderedConfig(_project, Token).Value.HostAccess;

        Assert.Equal(
            [new ComposeHostAccess("api_server", "network_mode: host"), new ComposeHostAccess("background", "privileged: true"), new ComposeHostAccess("web_server", "pid: host")],
            access);
    }

    [Fact, Trait("Lane", "unit")]
    public void RenderedConfig_output_it_cannot_read_is_011_and_never_echoes_the_environment()
    {
        var error = Cli(new FakeToolRunner(Exit(0, "not json hunter2-secret"))).RenderedConfig(_project, Token).Error;

        Assert.Equal("SND-ONYX-011", error.Code);
        Assert.DoesNotContain("hunter2", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_compose_call_unsets_the_compose_variables_and_every_key_sindri_writes_into_the_env()
    {
        var runner = new FakeToolRunner(Exit(0, "[]"));
        var cli = Cli(runner);

        _ = cli.Pull(_project, static _ => { }, Token);
        _ = cli.Up(_project, static _ => { }, limit: null, Token);
        _ = cli.Stop(_project, Token);
        _ = cli.Ps(_project, Token);
        _ = cli.RenderedConfig(_project, Token);

        string[] expected = ["COMPOSE_PROFILES", "COMPOSE_FILE", "COMPOSE_PROJECT_NAME", "COMPOSE_ENV_FILES", "COMPOSE_PATH_SEPARATOR", "IMAGE_TAG", "DISABLE_TELEMETRY", "AUTH_TYPE", "USER_AUTH_SECRET", "INDEXING_MODEL_SERVER_HOST", "HOST_PORT"];
        Assert.Equal(5, runner.Calls.Count);
        foreach (var call in runner.Calls)
        {
            Assert.NotNull(call.Environment);
            Assert.All(expected, name => Assert.True(call.Environment.ContainsKey(name) && call.Environment[name] is null, name));
        }
    }

    [Fact, Trait("Lane", "integration")]
    public void Every_compose_call_also_unsets_each_key_present_in_the_rendered_env_whatever_Sindri_wrote()
    {
        var directory = Path.Combine(Path.GetTempPath(), "aw-onyx-env-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(directory);
        try
        {
            var envFile = Path.Combine(directory, ".env");
            File.WriteAllText(envFile, "# a comment\nPOSTGRES_PASSWORD=password\n\nexport REDIS_PASSWORD=x\nHOST_PORT=3000\nQUOTED=\"a=b\"\n");
            var runner = new FakeToolRunner(Exit(0, "[]"));

            _ = Cli(runner).Stop(_project with { EnvFile = envFile }, Token);

            var environment = Assert.Single(runner.Calls).Environment!;
            Assert.All(["POSTGRES_PASSWORD", "REDIS_PASSWORD", "QUOTED", "HOST_PORT", "COMPOSE_FILE"], name => Assert.True(environment.ContainsKey(name) && environment[name] is null, $"{name} was not stripped"));
            Assert.DoesNotContain("a", environment.Keys, StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_read_only_docker_calls_keep_the_environment_as_it_is()
    {
        var runner = new FakeToolRunner(Exit(0, "linux/arm64"));

        _ = Cli(runner).ServerPlatform(Token);
        _ = Cli(runner).RepoDigests(["docker.io/x/y@sha256:aaa"], Token);

        Assert.All(runner.Calls, static c => Assert.Null(c.Environment));
    }

    [Fact, Trait("Lane", "unit")]
    public void Stop_uses_2_minutes_and_Ps_30_seconds_and_stop_removes_nothing()
    {
        var runner = new FakeToolRunner(Exit(0, "[]"));

        _ = Cli(runner).Stop(_project, Token);
        _ = Cli(runner).Ps(_project, Token);

        Assert.Equal([[.. _composePrefix, "stop"], [.. _composePrefix, "ps", "-a", "--format", "json"]], runner.Calls.Select(static c => c.Arguments));
        Assert.Equal([TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(30)], runner.Calls.Select(static c => c.Timeout));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_nonzero_compose_exit_is_011_with_the_last_stderr_lines()
    {
        var lines = Enumerable.Range(1, 8).Select(static n => $"line {n.ToString(System.Globalization.CultureInfo.InvariantCulture)}").ToArray();
        var runner = new FakeToolRunner(Exit(1), lines);

        var error = Cli(runner).Pull(_project, static _ => { }, Token).Error;

        Assert.Equal("SND-ONYX-011", error.Code);
        Assert.Contains("line 8", error.Message, StringComparison.Ordinal);
        Assert.Contains("line 4", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("line 3", error.Message, StringComparison.Ordinal);
        Assert.Contains("docker compose pull", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_compose_command_that_cannot_reach_the_daemon_is_001()
    {
        var runner = new FakeToolRunner(Exit(1), ["Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?"]);

        Assert.Equal("SND-ONYX-001", Cli(runner).Ps(_project, Token).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_missing_cli_is_001_and_a_timed_out_ps_is_001_but_a_timed_out_pull_is_011()
    {
        Assert.Equal("SND-ONYX-001", Cli(new FakeToolRunner(ToolRun.Missing)).Stop(_project, Token).Error.Code);
        Assert.Equal("SND-ONYX-001", Cli(new FakeToolRunner(ToolRun.TimedOut)).Ps(_project, Token).Error.Code);
        Assert.Equal("SND-ONYX-011", Cli(new FakeToolRunner(ToolRun.TimedOut)).Pull(_project, static _ => { }, Token).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cancelled_command_throws_after_the_tool_was_killed()
        => Assert.Throws<OperationCanceledException>(() => Cli(new FakeToolRunner(ToolRun.Cancelled)).Pull(_project, static _ => { }, Token));

    private const string NginxJson = "{\"Service\":\"nginx\",\"State\":\"running\",\"Health\":\"\",\"Image\":\"nginx@sha256:aaa\",\"Publishers\":[{\"URL\":\"127.0.0.1\",\"TargetPort\":80,\"PublishedPort\":3000,\"Protocol\":\"tcp\"}]}";
    private const string CacheJson = "{\"Service\":\"cache\",\"State\":\"exited\",\"Health\":\"\",\"Image\":\"redis:7.4-alpine\",\"Publishers\":null}";

    [Theory, Trait("Lane", "unit")]
    [InlineData(NginxJson + "\n" + CacheJson + "\n")]
    [InlineData("[" + NginxJson + "," + CacheJson + "]")]
    public void Ps_parses_one_json_object_per_line_and_a_json_array(string printed)
    {
        var containers = Cli(new FakeToolRunner(Exit(0, printed))).Ps(_project, Token).Value;

        Assert.Equal(["nginx", "cache"], containers.Select(static c => c.Service), StringComparer.Ordinal);
        Assert.Equal(("running", null, "nginx@sha256:aaa"), (containers[0].State, containers[0].Health, containers[0].Image));
        Assert.Equal([new ComposePublisher("127.0.0.1", 3000, 80)], containers[0].Publishers);
        Assert.Equal(("exited", null, "redis:7.4-alpine"), (containers[1].State, containers[1].Health, containers[1].Image));
        Assert.Empty(containers[1].Publishers);
    }

    [Fact, Trait("Lane", "unit")]
    public void Ps_keeps_a_wildcard_publisher_and_drops_one_that_is_not_published()
    {
        var printed = """{"Service":"nginx","State":"running","Health":"healthy","Image":"x","Publishers":[{"URL":"0.0.0.0","TargetPort":80,"PublishedPort":80,"Protocol":"tcp"},{"URL":"::","TargetPort":80,"PublishedPort":80,"Protocol":"tcp"},{"URL":"","TargetPort":9000,"PublishedPort":0,"Protocol":"tcp"}]}""";

        var container = Assert.Single(Cli(new FakeToolRunner(Exit(0, printed))).Ps(_project, Token).Value);

        Assert.Equal("healthy", container.Health);
        Assert.Equal([new ComposePublisher("0.0.0.0", 80, 80), new ComposePublisher("::", 80, 80)], container.Publishers);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("not json")]
    [InlineData("{\"Service\":")]
    public void Ps_reads_output_it_cannot_parse_as_011(string printed)
        => Assert.Equal("SND-ONYX-011", Cli(new FakeToolRunner(Exit(0, printed))).Ps(_project, Token).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void RepoDigests_normalizes_and_maps_a_missing_image_to_empty()
    {
        var runner = new FakeToolRunner((_, arguments) => arguments[^1] switch
        {
            "docker.io/library/nginx@sha256:aaa" => Exit(0, "[\"docker.io/library/nginx@sha256:aaa\"]\n"),
            "docker.io/onyxdotapp/onyx-backend@sha256:bbb" => Exit(0, "[\"onyxdotapp/onyx-backend@sha256:bbb\",\"docker.io/onyxdotapp/onyx-backend@sha256:ccc\"]\n"),
            _ => Exit(1),
        })
        {
            StandardErrorFor = static (_, arguments) => arguments[^1].Contains("gone", StringComparison.Ordinal) ? [$"Error response from daemon: No such image: {arguments[^1]}"] : [],
        };

        var digests = Cli(runner).RepoDigests(["docker.io/library/nginx@sha256:aaa", "docker.io/onyxdotapp/onyx-backend@sha256:bbb", "docker.io/x/gone@sha256:ddd"], Token).Value;

        Assert.Equal(["nginx@sha256:aaa"], digests["docker.io/library/nginx@sha256:aaa"]);
        Assert.Equal(["onyxdotapp/onyx-backend@sha256:bbb", "onyxdotapp/onyx-backend@sha256:ccc"], digests["docker.io/onyxdotapp/onyx-backend@sha256:bbb"]);
        Assert.Empty(digests["docker.io/x/gone@sha256:ddd"]);
        Assert.All(runner.Calls, static c => Assert.Equal(["image", "inspect", "--format", "{{json .RepoDigests}}"], c.Arguments.Take(4), StringComparer.Ordinal));
        Assert.All(runner.Calls, static c => Assert.Equal(TimeSpan.FromSeconds(30), c.Timeout));
    }

    [Fact, Trait("Lane", "unit")]
    public void RepoDigests_reports_a_failure_that_is_not_a_missing_image_instead_of_calling_it_not_pulled()
    {
        var runner = new FakeToolRunner(Exit(1), ["permission denied while trying to connect to the docker API at unix:///var/run/docker.sock"]);

        var error = Cli(runner).RepoDigests(["docker.io/x/y@sha256:aaa"], Token).Error;

        Assert.Equal("SND-ONYX-011", error.Code);
        Assert.Contains("permission denied", error.Message, StringComparison.Ordinal);
        Assert.Contains("image inspect", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_stop_gets_two_minutes_and_one_that_runs_out_is_011_cut_short_not_a_hung_docker()
    {
        var runner = new FakeToolRunner(ToolRun.TimedOut);

        var error = Cli(runner).Stop(_project, Token).Error;

        Assert.Equal(TimeSpan.FromMinutes(2), Assert.Single(runner.Calls).Timeout);
        Assert.Equal("SND-ONYX-011", error.Code);
        Assert.Contains("the stop was cut short", error.Message, StringComparison.Ordinal);
        Assert.Contains("sindri onyx down", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("isn't answering", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void RepoDigests_is_001_when_the_daemon_is_gone()
        => Assert.Equal("SND-ONYX-001", Cli(new FakeToolRunner(ToolRun.Missing)).RepoDigests(["a@sha256:b"], Token).Error.Code);

    private static readonly ComposeProject _ownProject = OnyxPaths.For(OnyxUpRig.Home).Project;

    private static FakeToolRunner Stats(string ours, ToolRun stats) => new((_, arguments) => arguments[0] == "ps" ? Ok(ours) : stats);

    [Fact, Trait("Lane", "unit")]
    public void The_memory_other_containers_use_leaves_out_this_projects_own_and_reads_every_unit()
    {
        var runner = Stats("aaaaaaaaaaaa\nbbbbbbbbbbbb", Ok("aaaaaaaaaaaa|7GiB / 15GiB\nbbbbbbbbbbbb|1GiB / 15GiB\ncccccccccccc|512MiB / 15GiB\ndddddddddddd|1024KiB / 15GiB\neeeeeeeeeeee|2048B / 15GiB\nffffffffffff|1.5GB / 15GiB\n111111111111|1MB / 15GiB"));

        var bytes = Cli(runner).MemoryUsedOutside(_ownProject, Token).Value;

        Assert.Equal((512L << 20) + (1L << 20) + 2048 + 1_500_000_000 + 1_000_000, bytes);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_memory_read_runs_ps_by_label_then_stats_with_no_stream_inside_five_seconds()
    {
        var runner = Stats("", Ok(""));

        _ = Cli(runner).MemoryUsedOutside(_ownProject, Token);

        Assert.Equal(["ps", "-q", "--filter", "label=com.docker.compose.project=" + _ownProject.Name], runner.Calls[0].Arguments, StringComparer.Ordinal);
        Assert.Equal(["stats", "--no-stream", "--format"], runner.Calls[1].Arguments.Take(3), StringComparer.Ordinal);
        Assert.Equal(TimeSpan.FromSeconds(5), runner.Calls[1].Timeout);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("exit")]
    [InlineData("timeout")]
    [InlineData("garbage")]
    public void A_failed_or_unreadable_stats_is_an_error_not_zero(string how)
    {
        var stats = how switch { "exit" => Fails, "timeout" => ToolRun.TimedOut, _ => Ok("cccccccccccc|lots") };

        Assert.True(Cli(Stats("", stats)).MemoryUsedOutside(_ownProject, Token).IsError);
    }
}
