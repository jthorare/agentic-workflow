using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Host.Commands.SessionWrap;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.SessionWrap;

/// <summary>'sindri session wrap' (API spec §8.1, launcher spec §3, §4) over fakes: the guard, the environment, the 30 s heartbeat, the exit call and the reported session id.</summary>
[Lane(TestLane.Unit)]
public sealed class SessionWrapTests : IDisposable
{
    private const string Uuid = "0a1b2c3d-0000-4000-8000-000000000001";
    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(20);
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeChildren _children = new();
    private readonly FakeIds _ids = new();
    private readonly StringWriter _errors = new();
    private readonly Dictionary<string, string?> _env = new(StringComparer.Ordinal)
    {
        ["PATH"] = "/usr/bin:/bin",
        ["AW_SINDRI_LAUNCH_SECRET"] = "wrap-proof",
        ["AW_SINDRI_ITEM"] = "ITM-00000000000000000000000000000001",
    };

    private readonly FakeWrapCalls _calls;

    public SessionWrapTests()
    {
        _calls = new FakeWrapCalls(_clock);
    }

    public void Dispose() => _errors.Dispose();

    private static string[] Args => ["--launch", A.FirstLaunchId.Value, "--", "/bin/sh", "-c", "agent"];

    private SessionWrapCommand Command(Task? hangup = null) => new(_calls, _children, name => _env.GetValueOrDefault(name), _clock, _errors, _ids, hangup);

    private static async Task Until(Func<bool> condition)
    {
        var limit = TimeProvider.System.GetTimestamp();
        while (!condition())
        {
            Assert.True(TimeProvider.System.GetElapsedTime(limit) < _deadline, "timed out waiting for the wrap");
            await Task.Delay(2, TestContext.Current.CancellationToken);
        }
    }

    private async Task Run(TimeSpan span)
    {
        for (var elapsed = TimeSpan.Zero; elapsed < span; elapsed += TimeSpan.FromMilliseconds(500))
        {
            _clock.Advance(TimeSpan.FromMilliseconds(500));
            await Task.Delay(2, TestContext.Current.CancellationToken);
        }
    }

    private async Task<int> Finish(Task<int> run, int code)
    {
        await Until(() => _children.Child is not null);
        _children.Child!.Exit(code);
        return await run.WaitAsync(_deadline, TimeProvider.System, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_hangup_ends_an_agent_that_ignores_it_after_the_grace_and_the_exit_is_reported()
    {
        var hangup = new TaskCompletionSource();
        var run = Command(hangup.Task).Run(Args, TestContext.Current.CancellationToken);
        await Until(() => _children.Child is not null);

        hangup.SetResult();
        await Run(TimeSpan.FromSeconds(6));
        var code = await run.WaitAsync(_deadline, TimeProvider.System, TestContext.Current.CancellationToken);

        Expect.True(_children.Child!.Killed);
        Expect.Equal(137, code);
        _ = Expect.Only(_calls.Exits);
    }

    [Fact]
    public async Task A_hangup_leaves_an_agent_that_ends_within_the_grace_alone()
    {
        var hangup = new TaskCompletionSource();
        var run = Command(hangup.Task).Run(Args, TestContext.Current.CancellationToken);
        await Until(() => _children.Child is not null);

        hangup.SetResult();
        await Run(TimeSpan.FromSeconds(1));
        _children.Child!.Exit(0);
        var code = await run.WaitAsync(_deadline, TimeProvider.System, TestContext.Current.CancellationToken);

        Expect.True(!_children.Child.Killed);
        Expect.Equal(0, code);
    }

    [Fact]
    public async Task The_secret_file_is_read_once_deleted_and_the_secret_reaches_the_agent_only()
    {
        var file = Path.Combine(Path.GetTempPath(), "wrap-secret-" + RealTime.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(file, "from-the-file\n", TestContext.Current.CancellationToken);
        _ = _env.Remove("AW_SINDRI_LAUNCH_SECRET");
        _env["AW_SINDRI_LAUNCH_SECRET_FILE"] = file;
        var run = Command().Run(Args, TestContext.Current.CancellationToken);

        var code = await Finish(run, 0);

        Expect.Equal(0, code);
        Expect.True(!File.Exists(file));
        Expect.Equal("from-the-file", _children.Environment!["AW_SINDRI_LAUNCH_SECRET"]);
        Expect.True(!_children.Environment.ContainsKey("AW_SINDRI_LAUNCH_SECRET_FILE"));
        Expect.Equal("from-the-file", _calls.Exits.Single().Proof);
    }

    [Fact]
    public async Task A_missing_secret_file_means_no_secret_and_no_heartbeats()
    {
        _ = _env.Remove("AW_SINDRI_LAUNCH_SECRET");
        _env["AW_SINDRI_LAUNCH_SECRET_FILE"] = Path.Combine(Path.GetTempPath(), "no-such-wrap-secret-" + RealTime.NewGuid().ToString("N"));
        var run = Command().Run(Args, TestContext.Current.CancellationToken);

        _ = await Finish(run, 0);

        Expect.True(_errors.ToString().Contains("AW_SINDRI_LAUNCH_SECRET is not set", StringComparison.Ordinal));
        Expect.True(_calls.Beats.Count == 0 && _calls.Exits.Count == 0 && !_children.Environment!.ContainsKey("AW_SINDRI_LAUNCH_SECRET"));
    }

    [Fact]
    public async Task Missing_bin_exits_127_with_003()
    {
        _env["PATH"] = "/nonexistent";

        var code = await Command().Run(["--launch", A.FirstLaunchId.Value, "--", "no-such-agent-cli"], TestContext.Current.CancellationToken);

        Expect.Equal(127, code);
        Expect.True(_errors.ToString().Contains("no-such-agent-cli not found (SND-LAUNCH-003)", StringComparison.Ordinal));
        Expect.True(_children.Child is null && _calls.Beats.Count == 0 && _calls.Exits.Count == 0);
    }

    [Fact]
    public async Task Bad_arguments_exit_2()
    {
        var code = await Command().Run(["--launch", "not-a-launch", "--", "sh"], TestContext.Current.CancellationToken);

        Expect.Equal(2, code);
        Expect.True(_errors.ToString().Contains("launch id", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_agent_gets_the_session_environment_and_its_own_command()
    {
        _env["AW_SINDRI_HOME"] = "/tmp/prover-home";
        var run = Command().Run(Args, TestContext.Current.CancellationToken);

        _ = await Finish(run, 0);

        Assert.Equal(["/bin/sh", "-c", "agent"], _children.Argv!, StringComparer.Ordinal);
        var set = _children.Environment!;
        Expect.Equal(("1", A.FirstLaunchId.Value, "ITM-00000000000000000000000000000001", "wrap-proof", "/tmp/prover-home"),
            (set["AW_SINDRI_CHILD"], set["AW_SINDRI_LAUNCH"], set["AW_SINDRI_ITEM"], set["AW_SINDRI_LAUNCH_SECRET"], set["AW_SINDRI_HOME"]));
    }

    [Fact]
    public async Task Exit_writes_session_ended_exit_code()
    {
        var run = Command().Run(Args, TestContext.Current.CancellationToken);

        var code = await Finish(run, 7);

        Expect.Equal(7, code);
        var exit = Assert.Single(_calls.Exits);
        Expect.Equal((7, null, "wrap-proof"), exit);
    }

    [Fact]
    public async Task A_heartbeat_goes_every_30s_until_the_agent_exits()
    {
        var run = Command().Run(Args, TestContext.Current.CancellationToken);
        await Until(() => _calls.Beats.Count == 1);

        await Run(TimeSpan.FromSeconds(95));
        var during = _calls.Beats.Count;
        _ = await Finish(run, 0);
        await Run(TimeSpan.FromSeconds(60));

        Expect.Equal((4, 4), (during, _calls.Beats.Count));
    }

    [Fact]
    public async Task The_heartbeat_carries_the_session_id_once_found_and_so_does_the_exit()
    {
        _ids.Script = look => look >= 3 ? new SessionIdScan(SessionId.From(Uuid), Ambiguous: false) : SessionIdScan.None;
        var run = Command().Run(Args, TestContext.Current.CancellationToken);
        await Until(() => _calls.Beats.Count == 1);

        await Run(TimeSpan.FromSeconds(65));
        _ = await Finish(run, 0);

        Expect.Equal((null, SessionId.From(Uuid), SessionId.From(Uuid)), (_calls.Beats[0].Session, _calls.Beats[1].Session, _calls.Beats[2].Session));
        Expect.Equal(Uuid, _calls.Exits.Single().Session!.Value.Value);
    }

    [Fact]
    public async Task Wrap_waits_at_most_30s_for_the_id()
    {
        var run = Command().Run(Args, TestContext.Current.CancellationToken);
        await Until(() => _calls.Beats.Count == 1);

        await Run(TimeSpan.FromSeconds(31));
        var atThirty = _ids.Looks;
        await Run(TimeSpan.FromSeconds(60));
        var later = _ids.Looks;
        _ = await Finish(run, 0);

        Expect.True(atThirty > 0);
        Expect.Equal(atThirty, later);
    }

    [Fact]
    public async Task Ambiguous_fallback_records_no_id()
    {
        _ids.Script = static _ => new SessionIdScan(Id: null, Ambiguous: true);
        var run = Command().Run(Args, TestContext.Current.CancellationToken);
        await Until(() => _calls.Beats.Count == 1);

        await Run(TimeSpan.FromSeconds(65));
        _ = await Finish(run, 0);

        Expect.True(_calls.Beats.All(static b => b.Session is null) && _calls.Exits.Single().Session is null);
        Expect.True(_errors.ToString().Contains("ambiguous session id", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Without_a_launch_secret_the_agent_runs_and_nothing_is_called()
    {
        _ = _env.Remove("AW_SINDRI_LAUNCH_SECRET");
        var run = Command().Run(Args, TestContext.Current.CancellationToken);

        var code = await Finish(run, 3);

        Expect.Equal(3, code);
        Expect.True(_calls.Beats.Count == 0 && _calls.Exits.Count == 0 && !_children.Environment!.ContainsKey("AW_SINDRI_LAUNCH_SECRET"));
    }

    [Fact]
    public async Task Refused_heartbeats_do_not_stop_the_agent_and_a_failed_exit_call_is_tried_three_times()
    {
        _calls.Refuse = SindriErrors.AuthLaunchSecret.With("the launch secret doesn't match an open launch");
        var run = Command().Run(Args, TestContext.Current.CancellationToken);
        await Until(() => _calls.Beats.Count == 1);
        await Run(TimeSpan.FromSeconds(35));
        await Until(() => _children.Child is not null);
        _children.Child!.Exit(5);

        var finishing = Run(TimeSpan.FromSeconds(10));
        var code = await run.WaitAsync(_deadline, TimeProvider.System, TestContext.Current.CancellationToken);
        await finishing;

        Expect.Equal((5, 3), (code, _calls.Exits.Count));
    }
}
