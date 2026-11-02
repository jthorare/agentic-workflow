using Aw.Kernel.Testing;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Host.Commands.SessionWrap;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.SessionWrap;

/// <summary>'session wrap' clears the heavy wrapper's dead slots once the scope has ended (sandbox spec §15 item 8), and a failing sweep never changes the exit.</summary>
[Lane(TestLane.Unit)]
public sealed class SessionWrapSweepTests : IDisposable
{
    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(20);
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeChildren _children = new();
    private readonly StringWriter _errors = new();
    private readonly FakeWrapCalls _calls;

    public SessionWrapSweepTests()
    {
        _calls = new FakeWrapCalls(_clock);
    }

    public void Dispose() => _errors.Dispose();

    private sealed class RecordingSweep(bool fail = false) : IHeavyLockSweep
    {
        public List<string> Dirs { get; } = [];

        public int Sweep(string locksDir)
        {
            Dirs.Add(locksDir);
            return fail ? throw new IOException("boom") : 0;
        }
    }

    private async Task<int> RunTo(IHeavyLockSweep sweep, int exit)
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal) { ["PATH"] = "/usr/bin:/bin", ["AW_SINDRI_LAUNCH_SECRET"] = "wrap-proof" };
        var command = new SessionWrapCommand(_calls, _children, name => env.GetValueOrDefault(name), _clock, _errors, new FakeIds(), new WrapExtras(Sweep: sweep, LocksDir: "/state/locks"));
        var run = command.Run(["--launch", A.FirstLaunchId.Value, "--", "/bin/sh", "-c", "agent"], TestContext.Current.CancellationToken);
        var limit = TimeProvider.System.GetTimestamp();
        while (_children.Child is null)
        {
            Assert.True(TimeProvider.System.GetElapsedTime(limit) < _deadline, "timed out waiting for the child");
            await Task.Delay(2, TestContext.Current.CancellationToken);
        }

        _children.Child.Exit(exit);
        return await run.WaitAsync(_deadline, TimeProvider.System, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task The_exit_sweeps_the_state_locks_folder_once()
    {
        var sweep = new RecordingSweep();

        var code = await RunTo(sweep, 0);

        Expect.Equal(0, code);
        Expect.Equal("/state/locks", Expect.Only(sweep.Dirs));
    }

    [Fact]
    public async Task A_sweep_that_fails_does_not_change_the_agents_exit_code()
    {
        var sweep = new RecordingSweep(fail: true);

        var code = await RunTo(sweep, 7);

        Expect.Equal(7, code);
        Expect.Equal(1, sweep.Dirs.Count);
    }
}
