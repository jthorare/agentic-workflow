using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Infrastructure.Toolkits;
using Aw.Sindri.Tests.BuildToolkit;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Toolkits;

/// <summary>The toolkit supervisor (agent-first PR 16): restart with backoff, three crashes in ten minutes make it 'failed', a stop is not a crash.</summary>
[Lane(TestLane.Unit)]
public sealed class ToolkitSupervisorTests : IAsyncDisposable
{
    private static readonly Toolkit _built = new("triage", "/tk/triage", ToolkitState.Built, SourceHash: "h", BuiltAt: DateTimeOffset.UnixEpoch, Manifest: null, CreatedBy: "human");

    private readonly FakeTimeProvider _clock = new();
    private readonly ToolkitRig.FakeStore _store = new();
    private readonly FakeLauncher _launcher = new();
    private readonly ToolkitSupervisor _supervisor;

    public ToolkitSupervisorTests()
    {
        _store.Items["triage"] = _built;
        _supervisor = new ToolkitSupervisor(_launcher, _store, _clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => _supervisor.DisposeAsync();

    [Fact]
    public async Task A_crashing_toolkit_restarts_with_backoff_and_is_marked_failed_after_three_crashes()
    {
        var started = await _supervisor.Start(_built, Token);
        Assert.True(started.IsSuccess);
        Assert.Equal(1, _launcher.Launched);

        _launcher.Last.Crash();   // crash 1: back after 1 s
        await Eventually(() => _launcher.Launched == 2, advance: TimeSpan.FromSeconds(1));
        _launcher.Last.Crash();   // crash 2: back after 2 s
        await Eventually(() => _launcher.Launched == 3, advance: TimeSpan.FromSeconds(1));
        _launcher.Last.Crash();   // crash 3 inside ten minutes: failed, no fourth start
        await Eventually(() => _store.Items["triage"].State == ToolkitState.Failed, advance: TimeSpan.FromSeconds(1));

        Assert.Equal(3, _launcher.Launched);
        Assert.Equal(ToolkitState.Failed, _store.Items["triage"].State);
    }

    [Fact]
    public async Task A_stop_ends_the_process_and_does_not_restart_it()
    {
        _ = await _supervisor.Start(_built, Token);
        var process = _launcher.Last;

        var stopped = await _supervisor.Stop("triage", Token);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await Task.Delay(50, Token);

        Assert.True(stopped.IsSuccess);
        Assert.True(process.Disposed);
        Assert.Equal(1, _launcher.Launched);
        Assert.NotEqual(ToolkitState.Failed, _store.Items["triage"].State);
    }

    // The watcher runs on its own task: tick the fake clock and wait in small real steps until the condition holds.
    private async Task Eventually(Func<bool> condition, TimeSpan advance)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            _clock.Advance(advance);
            await Task.Delay(10, Token);
        }

        Assert.True(condition());
    }

    private sealed class FakeLauncher : IToolkitProcessLauncher
    {
        private readonly List<FakeProcess> _all = [];

        internal int Launched => _all.Count;

        internal FakeProcess Last => _all[^1];

        public ValueTask<Result<IToolkitProcess>> Launch(Toolkit toolkit, CancellationToken cancellationToken)
        {
            var process = new FakeProcess();
            _all.Add(process);
            return ValueTask.FromResult<Result<IToolkitProcess>>(process);
        }
    }

    private sealed class FakeProcess : IToolkitProcess
    {
        private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Exited => _exited.Task;

        internal bool Disposed { get; private set; }

        internal void Crash() => _exited.TrySetResult();

        public ValueTask<Result<string>> Call(string tool, string inputJson, TimeSpan timeout, CancellationToken cancellationToken) => ValueTask.FromResult<Result<string>>("{}");

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            _ = _exited.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }
}
