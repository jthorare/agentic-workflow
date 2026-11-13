using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The real tmux adapter on a private server (API P10a, U5): the shared contract plus what only a real tmux can show.</summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class TmuxSessionHostTests : SessionHostContract, IDisposable
{
    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(4);
    private readonly PrivateTmux _tmux = new();

    public void Dispose() => _tmux.Dispose();

    protected override ISessionHost Create() => new TmuxSessionHost(_tmux.Options);

    protected override HostSession Session(string name) =>
        new(name, _tmux.Scratch, ["sleep", "60"], new Dictionary<string, string>(StringComparer.Ordinal));

    [Fact]
    public async Task Start_makes_a_live_session_and_Stop_removes_it()
    {
        var host = Create();

        var started = await host.Start(Session("snd-live"), _deadline, CancellationToken.None);
        Expect.True(started.IsSuccess);
        Expect.True(_tmux.HasSession("snd-live"));

        _ = await host.Stop(started.Value, CancellationToken.None);
        Expect.True(!_tmux.HasSession("snd-live"));
    }

    [Fact]
    public void The_attach_argv_names_the_private_socket_and_the_exact_session()
    {
        var argv = Create().AttachArgv(new HostRef(HostKind.Tmux, "snd-one"));

        Expect.Sequence(["tmux", "-L", _tmux.Socket, "attach", "-t", "=snd-one"], argv);
    }

    [Fact]
    public async Task The_session_environment_reaches_the_process()
    {
        var marker = Path.Combine(_tmux.Scratch, "env.txt");
        var env = new Dictionary<string, string>(StringComparer.Ordinal) { ["SND_PROBE_VALUE"] = "from the host" };
        var session = new HostSession("snd-env", _tmux.Scratch, ["sh", "-c", $"printf %s \"$SND_PROBE_VALUE\" > '{marker}'; sleep 60"], env);

        var started = await Create().Start(session, _deadline, CancellationToken.None);

        Expect.True(started.IsSuccess);
        Expect.Equal("from the host", await ReadWhenWritten(marker));
    }

    [Fact]
    public async Task The_session_starts_in_its_work_dir()
    {
        var marker = Path.Combine(_tmux.Scratch, "cwd.txt");
        var session = new HostSession("snd-cwd", _tmux.Scratch, ["sh", "-c", $"pwd -P > '{marker}'; sleep 60"], new Dictionary<string, string>(StringComparer.Ordinal));

        _ = await Create().Start(session, _deadline, CancellationToken.None);

        Expect.Equal(CanonicalPath.Of(_tmux.Scratch), (await ReadWhenWritten(marker)).Trim());
    }

    [Fact]
    public async Task AttachedClients_counts_an_attached_client()
    {
        var host = Create();
        var started = await host.Start(Session("snd-attach"), _deadline, CancellationToken.None);
        using var client = _tmux.Client("snd-attach");
        try
        {
            Expect.Equal(1, await CountWhen(host, started.Value, 1));
        }
        finally
        {
            client.Kill();   // by process, never by name
            await client.WaitForExitAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task A_start_past_its_deadline_fails_and_leaves_no_session()
    {
        var started = await Create().Start(Session("snd-late"), TimeSpan.FromMilliseconds(1), CancellationToken.None);

        Expect.True(started.IsError && started.Error.Code == SindriErrors.LaunchTimedOut.Code);
        Expect.True(!_tmux.HasSession("snd-late"));
    }

    [Fact]
    public async Task Never_uses_set_environment_g()
    {
        var fake = FakeTmux.Install(_tmux.Scratch, "tmux 3.4");
        var env = new Dictionary<string, string>(StringComparer.Ordinal) { ["SND_A"] = "1", ["SND_B"] = "two words" };
        var session = new HostSession("snd-fake", _tmux.Scratch, ["claude", "--flag"], env);

        _ = await new TmuxSessionHost(new TmuxHostOptions(fake.Path, "sindri-launch", _tmux.TmuxTmpDir)).Start(session, _deadline, CancellationToken.None);

        var calls = fake.Calls();
        Expect.True(calls.All(c => !c.Contains("set-environment", StringComparison.Ordinal) && !c.Contains("setenv", StringComparison.Ordinal)));
        var create = Expect.Only([.. calls.Where(c => c.Contains("new-session", StringComparison.Ordinal))]);
        Expect.True(create.StartsWith("-L sindri-launch new-session -d -s snd-fake -c ", StringComparison.Ordinal));
        Expect.True(!create.Contains("-e ", StringComparison.Ordinal));   // the env travels in the launch script, not on the tmux line
        var script = await File.ReadAllLinesAsync(create[(create.LastIndexOf(" sh ", StringComparison.Ordinal) + 4)..], TestContext.Current.CancellationToken);
        Expect.True(script.Contains("export SND_A='1'") && script.Contains("export SND_B='two words'"));
        Expect.True(script.Any(l => l.StartsWith("exec ", StringComparison.Ordinal) && l.EndsWith("'claude' '--flag'", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("tmux 3.2", "3.2")]
    [InlineData("tmux 3.2a", "3.2")]
    [InlineData("tmux next-3.5", "3.5")]
    [InlineData("tmux master", "master")]
    public async Task Tmux_3_2_and_newer_and_development_builds_are_usable(string banner, string version)
    {
        var fake = FakeTmux.Install(_tmux.Scratch, banner);

        var facts = await new TmuxSessionHost(new TmuxHostOptions(fake.Path, "sindri-launch")).Probe(CancellationToken.None);

        Expect.Equal(new HostFacts(HostKind.Tmux, Present: true, version, Problem: null), facts);
    }

    [Theory]
    [InlineData("error connecting to /tmp/tmux-1000/sindri-launch (No such file or directory)", true)]
    [InlineData("error connecting to /tmp/tmux-1000/sindri-launch (Connection refused)", true)]
    [InlineData("no server running on /tmp/tmux-1000/sindri-launch", true)]
    [InlineData("can't find session: snd-x", true)]
    [InlineData("error connecting to /tmp/tmux-1000/sindri-launch (Permission denied)", false)]
    [InlineData("server exited unexpectedly", false)]
    public async Task Stop_treats_only_a_session_that_is_really_gone_as_stopped(string complaint, bool gone)
    {
        var path = Path.Combine(_tmux.Scratch, "kill-fails");
        _ = ExecutableScript.Write(path, $"case \"$*\" in\n  *kill-session*) echo \"{complaint}\" >&2; exit 1 ;;\nesac\nexit 0\n");

        var stopped = await new TmuxSessionHost(new TmuxHostOptions(path, "sindri-launch")).Stop(new HostRef(HostKind.Tmux, "snd-x"), CancellationToken.None);

        Expect.Equal(gone, stopped.IsSuccess);   // a refused socket is not "gone": the caller keeps the secrets of a session that may still run
    }

    // #3434: a kill that reaches a server while it exits (its last session just ended) gets "server exited unexpectedly" or "no current target"
    // (22 of 600 real tmux 3.4 runs); asked again, the same server says "no server running".
    [Theory]
    [InlineData("server exited unexpectedly")]
    [InlineData("no current target")]
    public async Task Stop_asks_again_when_the_server_was_exiting(string complaint)
    {
        var path = Path.Combine(_tmux.Scratch, "kill-mid-exit");
        var asked = Path.Combine(_tmux.Scratch, "asked-once");
        _ = ExecutableScript.Write(path,
            $"case \"$*\" in\n  *kill-session*) if [ -e '{asked}' ]; then echo 'no server running on /tmp/tmux-1000/sindri-launch' >&2; else : > '{asked}'; echo '{complaint}' >&2; fi; exit 1 ;;\nesac\nexit 0\n");

        var stopped = await new TmuxSessionHost(new TmuxHostOptions(path, "sindri-launch")).Stop(new HostRef(HostKind.Tmux, "snd-x"), CancellationToken.None);

        Expect.True(stopped.IsSuccess);
    }

    [Fact]
    public async Task One_deadline_covers_the_version_check_and_the_new_session()
    {
        var path = Path.Combine(_tmux.Scratch, "slow-tmux");
        _ = ExecutableScript.Write(path, "sleep 0.6\ncase \"$*\" in\n  -V) echo 'tmux 3.4' ;;\nesac\nexit 0\n");
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var result = await new TmuxSessionHost(new TmuxHostOptions(path, "sindri-launch")).Start(Session("snd-slow"), TimeSpan.FromSeconds(1), CancellationToken.None);

        Expect.True(result.IsError && result.Error.Code == SindriErrors.LaunchTimedOut.Code);   // 0.6 s + 0.6 s is over the 1 s the host stage gets; each alone is not
        Expect.True(clock.Elapsed < TimeSpan.FromSeconds(4));
    }

    [Theory]
    [InlineData("tmux 2.9", "2.9")]
    [InlineData("tmux 3.1c", "3.1")]
    public async Task Tmux_older_than_3_2_is_a_host_problem(string banner, string version)
    {
        var fake = FakeTmux.Install(_tmux.Scratch, banner);
        var host = new TmuxSessionHost(new TmuxHostOptions(fake.Path, "sindri-launch"));

        var facts = await host.Probe(CancellationToken.None);
        var started = await host.Start(Session("snd-old"), _deadline, CancellationToken.None);

        Expect.Equal(new HostFacts(HostKind.Tmux, Present: false, version, "too-old"), facts);
        Expect.True(started.IsError && started.Error.Code == SindriErrors.LaunchTimedOut.Code);
        Expect.True(fake.Calls().All(c => !c.Contains("new-session", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_missing_tmux_is_a_host_problem()
    {
        var host = new TmuxSessionHost(new TmuxHostOptions(Path.Combine(_tmux.Scratch, "no-such-tmux")));

        var facts = await host.Probe(CancellationToken.None);

        Expect.Equal(new HostFacts(HostKind.Tmux, Present: false, Version: null, "not-installed"), facts);
    }

    [Fact]
    public async Task The_probe_finds_a_supported_tmux()
    {
        var facts = await Create().Probe(CancellationToken.None);

        Expect.True(facts.Present && facts.Problem is null && facts.Version is not null);
    }

    private static async Task<string> ReadWhenWritten(string path)
    {
        for (var i = 0; i < 100; i++)
        {
            if (File.Exists(path) && new FileInfo(path).Length > 0)
            {
                return await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"{path} was never written");
    }

    private static async Task<int> CountWhen(ISessionHost host, HostRef session, int wanted)
    {
        var count = 0;
        for (var i = 0; i < 100 && count != wanted; i++)
        {
            count = await host.AttachedClients(session, CancellationToken.None);
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return count;
    }

    [Fact]
    public async Task A_launch_secret_reaches_the_process_but_never_tmuxs_environment_or_the_pane_command()
    {
        const string Value = "launch-proof-0123456789abcdef";
        var marker = Path.Combine(_tmux.Scratch, "secret.txt");
        var host = Create();
        var session = new HostSession("snd-proof", _tmux.Scratch, ["sh", "-c", $"printf %s \"$AW_SINDRI_LAUNCH_SECRET\" > '{marker}'; sleep 60"], new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_SINDRI_LAUNCH_SECRET"] = Value });

        var started = await host.Start(session, _deadline, CancellationToken.None);

        Expect.True(started.IsSuccess);
        Expect.Equal(Value, await ReadWhenWritten(marker));
        var global = _tmux.Run("show-environment", "-g");
        var scoped = _tmux.Run("show-environment", "-t", "=snd-proof");
        var commands = _tmux.Run("list-panes", "-a", "-F", "#{pane_start_command}|#{pane_current_command}");
        Expect.True(global is not null && !global.Contains("AW_SINDRI_LAUNCH_SECRET", StringComparison.Ordinal) && !global.Contains(Value, StringComparison.Ordinal));
        Expect.True(scoped is not null && !scoped.Contains(Value, StringComparison.Ordinal));   // the launch script exports it inside the pane (#1765): tmux never holds it
        Expect.True(commands is not null && !commands.Contains(Value, StringComparison.Ordinal));
    }

    [Fact]
    public async Task PaneId_answers_the_pane_of_a_live_session_and_null_for_a_gone_one()
    {
        var host = Create();
        var started = await host.Start(Session("sindri-pane0001"), _deadline, CancellationToken.None);

        var pane = await ((ISessionPanes)host).PaneId(started.Value, CancellationToken.None);
        _ = await host.Stop(started.Value, CancellationToken.None);
        var gone = await ((ISessionPanes)host).PaneId(started.Value, CancellationToken.None);

        Assert.Matches("^%[0-9]+$", pane?.Value ?? string.Empty);
        Assert.Null(gone);
        Expect.Equal(_tmux.Socket, ((ISessionPanes)host).Socket);
    }

    [Fact]
    public async Task A_session_with_a_size_and_remain_on_exit_keeps_its_pane_after_the_agent_exits()
    {
        var session = new HostSession("sindri-stay0001", _tmux.Scratch, ["sh", "-c", "sleep 1"], new Dictionary<string, string>(StringComparer.Ordinal)) { Size = new HostSize(100, 30), RemainOnExit = true };

        var started = await Create().Start(session, _deadline, CancellationToken.None);

        Expect.True(started.IsSuccess);
        Expect.Equal("1", await PaneDeadWhenSet("sindri-stay0001"));   // the process ended; the pane stays so the view can show Ended
        Expect.Equal("100x30", _tmux.Run("list-windows", "-t", "=sindri-stay0001", "-F", "#{window_width}x#{window_height}")?.Trim());
    }

    [Fact]
    public async Task An_agent_that_dies_at_once_keeps_its_dead_pane()
    {
        var session = new HostSession("sindri-dies0001", _tmux.Scratch, ["false"], new Dictionary<string, string>(StringComparer.Ordinal)) { RemainOnExit = true };

        var started = await Create().Start(session, _deadline, CancellationToken.None);

        Expect.True(started.IsSuccess);
        Expect.Equal("1", await PaneDeadWhenSet("sindri-dies0001"));   // why: remain-on-exit lands in the new-session call, before tmux reaps 'false' (#3466)
    }

    private async Task<string?> PaneDeadWhenSet(string name)
    {
        string? dead = null;
        var deadline = RealTime.NowOffset + TimeSpan.FromSeconds(10);
        while (RealTime.NowOffset < deadline && dead != "1")
        {
            dead = _tmux.HasSession(name) ? _tmux.Run("list-panes", "-t", "=" + name, "-F", "#{pane_dead}")?.Trim() : null;
            if (dead != "1")
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
        }

        return dead;
    }

    [Fact]
    public async Task A_clean_environment_session_gets_only_its_own_variables()
    {
        var marker = Path.Combine(_tmux.Scratch, "clean.txt");
        var env = new Dictionary<string, string>(StringComparer.Ordinal) { ["PATH"] = "/usr/bin:/bin", ["KEPT_VALUE"] = "yes" };
        var session = new HostSession("sindri-clean001", _tmux.Scratch, ["sh", "-c", $"env | sort > '{marker}'; sleep 60"], env) { CleanEnvironment = true };

        var started = await Create().Start(session, _deadline, CancellationToken.None);

        Expect.True(started.IsSuccess);
        var seen = await ReadWhenWritten(marker);
        Expect.True(seen.Contains("KEPT_VALUE=yes", StringComparison.Ordinal) && seen.Contains("TERM=", StringComparison.Ordinal));
        Expect.True(!seen.Contains("AW_", StringComparison.Ordinal));
    }

    /// <summary>A stand-in 'tmux' that logs its arguments and prints a version banner; every session is new to it.</summary>
    private sealed record FakeTmux(string Path, string Log)
    {
        public static FakeTmux Install(string dir, string banner)
        {
            var path = System.IO.Path.Combine(dir, "fake-tmux");
            var log = System.IO.Path.Combine(dir, "fake-tmux.log");
            _ = ExecutableScript.Write(path, $"printf '%s\\n' \"$*\" >> '{log}'\ncase \"$*\" in\n  -V) echo '{banner}' ;;\n  *has-session*) exit 1 ;;\nesac\nexit 0\n");
            return new FakeTmux(path, log);
        }

        public string[] Calls() => File.Exists(Log) ? File.ReadAllLines(Log) : [];
    }
}
