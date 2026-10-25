using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Host.Commands.SessionWrap;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.SessionWrap;

/// <summary>'sindri session wrap --limits' (sandbox spec §3.3, SB7): the agent runs under a watchdog, and a session a limit ended is reported as a limit, never as an ordinary exit or as lost.</summary>
[Lane(TestLane.Unit)]
public sealed class SessionWrapLimitsTests : IDisposable
{
    private const long Gib = 1024L * 1024 * 1024;
    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(20);
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeChildren _children = new();
    private readonly FakeIds _ids = new();
    private readonly StringWriter _errors = new();
    private readonly FakeScope _scope = new();
    private readonly FakeWrapCalls _calls;
    private readonly Dictionary<string, string?> _env = new(StringComparer.Ordinal)
    {
        ["PATH"] = "/usr/bin:/bin",
        ["AW_SINDRI_LAUNCH_SECRET"] = "wrap-proof",
        ["AW_SINDRI_ITEM"] = "ITM-00000000000000000000000000000001",
    };

    public SessionWrapLimitsTests()
    {
        _calls = new FakeWrapCalls(_clock);
        _scope.OnKill = _ => _children.Child?.Exit(137);
    }

    public void Dispose() => _errors.Dispose();

    private static string[] Args => ["--launch", A.FirstLaunchId.Value, "--limits", "/run/limits.json", "--", "/bin/sh", "-c", "agent"];

    private static WrapLimits Limits(long? wall = 120) => new("sindri-unit.scope", wall, WallGraceSeconds: 60, ParkMaxSeconds: 86400, MemoryMaxBytes: 4 * Gib);

    private SessionWrapCommand Command(FakeLimitsSource source) => new(_calls, _children, name => _env.GetValueOrDefault(name), _clock, _errors, _ids, hangup: null, source);

    private static FakeLimitsSource Source(WrapLimits limits, FakeScope scope) => new(new WrapLimitsSetup(limits, scope));

    private static async Task Until(Func<bool> condition)
    {
        var limit = TimeProvider.System.GetTimestamp();
        while (!condition())
        {
            Assert.True(TimeProvider.System.GetElapsedTime(limit) < _deadline, "timed out waiting for the wrap");
            await Task.Delay(2, TestContext.Current.CancellationToken);
        }
    }

    private async Task Advance(TimeSpan span)
    {
        for (var elapsed = TimeSpan.Zero; elapsed < span; elapsed += TimeSpan.FromSeconds(1))
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(1, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void Arguments_can_name_a_limits_file()
    {
        var parsed = SessionWrapArguments.Parse(["--launch", A.FirstLaunchId.Value, "--limits", "/run/limits.json", "--", "claude", "--resume"]);

        Expect.True(parsed.IsSuccess);
        Expect.Equal(("/run/limits.json", "claude"), (parsed.Value.LimitsFile, parsed.Value.Argv[0]));
    }

    [Fact]
    public void Arguments_with_limits_but_no_file_are_refused() =>
        Expect.True(SessionWrapArguments.Parse(["--launch", A.FirstLaunchId.Value, "--limits", "--", "claude"]).IsError);

    [Fact]
    public async Task A_session_that_uses_up_its_wall_is_stopped_and_reported_as_a_wall_limit()
    {
        var source = Source(Limits(wall: 120), _scope);
        var run = Command(source).Run(Args, TestContext.Current.CancellationToken);
        await Until(() => _children.Child is not null);

        await Advance(TimeSpan.FromSeconds(130));
        var code = await run.WaitAsync(_deadline, TimeProvider.System, TestContext.Current.CancellationToken);

        Expect.Equal("/run/limits.json", source.Opened!);
        Expect.Equal(137, code);
        Expect.Equal(0, _calls.Exits.Count);
        var exit = Expect.Only(_calls.LimitExits);
        Expect.True(exit.Limit is { Kind: LimitKind.Wall, Limit: 120 });
        Expect.Equal(("wrap-proof", 137), (exit.Proof, exit.Code));
    }

    [Fact]
    public async Task A_session_the_kernel_killed_for_memory_is_reported_as_a_memory_limit()
    {
        _ = new FakeScope();
        var run = Command(Source(Limits(), _scope)).Run(Args, TestContext.Current.CancellationToken);
        await Until(() => _children.Child is not null);
        _scope.Current = FakeScope.Reading(memory: 4 * Gib, peak: 4 * Gib, oomKill: 1);
        await Advance(TimeSpan.FromSeconds(6));

        _children.Child!.Exit(137);
        _ = await run.WaitAsync(_deadline, TimeProvider.System, TestContext.Current.CancellationToken);

        var exit = Expect.Only(_calls.LimitExits);
        Expect.True(exit.Limit is { Kind: LimitKind.Memory } && exit.Limit.Limit == 4 * Gib && exit.Limit.Peak == 4 * Gib);
        Expect.Equal(0, _calls.Exits.Count);
    }

    [Fact]
    public async Task An_ordinary_exit_of_a_limited_session_is_a_plain_exit()
    {
        var run = Command(Source(Limits(), _scope)).Run(Args, TestContext.Current.CancellationToken);
        await Until(() => _children.Child is not null);

        _children.Child!.Exit(0);
        var code = await run.WaitAsync(_deadline, TimeProvider.System, TestContext.Current.CancellationToken);

        Expect.Equal(0, code);
        Expect.Equal(0, _calls.LimitExits.Count);
        _ = Expect.Only(_calls.Exits);
    }

    [Fact]
    public async Task A_limits_file_that_cannot_be_used_refuses_the_start_and_runs_no_agent()
    {
        var source = new FakeLimitsSource(SindriErrors.InvalidInput.With("limits.json is not JSON"));

        var code = await Command(source).Run(Args, TestContext.Current.CancellationToken);

        Expect.Equal(2, code);
        Expect.True(_children.Child is null);
        Expect.True(_errors.ToString().Contains("limits.json is not JSON", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_session_with_no_limits_file_is_not_watched()
    {
        var source = Source(Limits(), _scope);
        var run = Command(source).Run(["--launch", A.FirstLaunchId.Value, "--", "/bin/sh", "-c", "agent"], TestContext.Current.CancellationToken);
        await Until(() => _children.Child is not null);

        await Advance(TimeSpan.FromMinutes(10));
        _children.Child!.Exit(0);
        _ = await run.WaitAsync(_deadline, TimeProvider.System, TestContext.Current.CancellationToken);

        Expect.True(source.Opened is null);
        Expect.Equal(0, _calls.SuspensionReads);
        Expect.Equal(0, _scope.Kills.Count);
    }
}
