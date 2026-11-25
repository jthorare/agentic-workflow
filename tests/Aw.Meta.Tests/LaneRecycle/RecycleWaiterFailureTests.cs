using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;
using Aw.Meta.Features.LaneRecycle;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.LaneRecycle;

// #2498 part 1: the detached waiter's output goes to /dev/null, so every failed exit has to leave a NOTE in the running log.
public sealed class RecycleWaiterFailureTests
{
    private const string Digest = "/home/x/digests/t1-meta-2026-11-09.md";
    private const string OldSession = "s-meta-old";
    private readonly MetaWorld _world = new();
    private readonly InMemoryWaiterLauncher _launcher = new();

    private async Task Ready(int digestAgeMinutes)
    {
        await _world.Ready();
        _world.LaneFiles.Put(new LaneRecord("meta", SessionId.From(OldSession), Agent: null, A.At(1)));
        _world.LaneSessions.Set("t1-meta", OldSession);
        _world.Panes.Put("t1-meta", "claude");
        _world.ClaudeTranscripts.PutUsage(OldSession, 410_000, A.At(30));
        _world.Digests.Put(Digest, 1200, _world.Clock.GetUtcNow().AddMinutes(-digestAgeMinutes));
    }

    private LaneRecycleService Service(IDelay? delay = null)
    {
        var wait = delay ?? _world.Delay;
        var runner = new RecycleRunner(new PaneSubmitter(_world.Panes, wait), _world.LaneSessions, _world.LaneFiles, _world.Log, wait, _world.Clock, new RecycleSettler(_world.Messages, _world.Sync, _world.Clock));
        return new LaneRecycleService(
            _world.Config,
            new RecycleGuard(_world.LaneFiles, _world.LaneSessions, _world.Digests, _world.Context, _world.Clock),
            runner,
            new SwitchRunner(new PaneSubmitter(_world.Panes, wait), _world.LaneSessions, _world.Runner, _world.Log, wait, _world.Clock),
            new RecycleWaiter(_launcher, _world.LaneFiles, new InMemoryProcessLiveness(), new LaneBeats(_world.Values, wait), runner, _world.Log, _world.Clock));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_runner_failure_in_the_waiter_logs_a_note_with_the_code_and_the_manual_tmux_line_and_clears_the_waiter_record()
    {
        await Ready(digestAgeMinutes: 5);
        var start = _world.Clock.GetUtcNow();
        _ = await _world.LaneFiles.RecordWaiter("meta", (4242, start), CancellationToken.None);
        _ = await _world.Beats.Record("meta", stopped: true, start.AddMinutes(1), CancellationToken.None);   // the turn ends; no new session ever shows up

        var result = await Service().Handle(new LaneRecycleRequest("meta", Digest, DryRun: false, Detach: false, WaitIdle: true), CancellationToken.None);

        Assert.Equal(MetaErrors.RecycleTimedOut.Code, result.Error.Code);
        var note = Assert.Single(_world.Log.Lines);
        Assert.StartsWith("- NOTE ", note, StringComparison.Ordinal);
        Assert.Contains("meta", note, StringComparison.Ordinal);
        Assert.Contains("AW-META-022", note, StringComparison.Ordinal);
        Assert.Contains("tmux -L t1 send-keys -t '=t1-meta:'", note, StringComparison.Ordinal);
        Assert.Null((await _world.LaneFiles.Waiter("meta", CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_guard_refusal_when_the_waiter_starts_logs_a_note_clears_the_waiter_record_and_types_nothing()
    {
        await Ready(digestAgeMinutes: 180);   // older than the 2 hours the guard accepts
        _ = await _world.LaneFiles.RecordWaiter("meta", (4242, _world.Clock.GetUtcNow()), CancellationToken.None);

        var result = await Service().Handle(new LaneRecycleRequest("meta", Digest, DryRun: false, Detach: false, WaitIdle: true), CancellationToken.None);

        Assert.Equal(MetaErrors.RecycleRefused.Code, result.Error.Code);
        var note = Assert.Single(_world.Log.Lines);
        Assert.StartsWith("- NOTE ", note, StringComparison.Ordinal);
        Assert.Contains("AW-META-021", note, StringComparison.Ordinal);
        Assert.Null((await _world.LaneFiles.Waiter("meta", CancellationToken.None)).Value);
        Assert.Empty(_world.Panes.Woken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_detach_whose_launcher_fails_records_no_waiter()
    {
        await Ready(digestAgeMinutes: 5);
        var failing = new FailingLauncher();
        var runner = new RecycleRunner(new PaneSubmitter(_world.Panes, _world.Delay), _world.LaneSessions, _world.LaneFiles, _world.Log, _world.Delay, _world.Clock, new RecycleSettler(_world.Messages, _world.Sync, _world.Clock));
        var service = new LaneRecycleService(
            _world.Config,
            new RecycleGuard(_world.LaneFiles, _world.LaneSessions, _world.Digests, _world.Context, _world.Clock),
            runner,
            new SwitchRunner(new PaneSubmitter(_world.Panes, _world.Delay), _world.LaneSessions, _world.Runner, _world.Log, _world.Delay, _world.Clock),
            new RecycleWaiter(failing, _world.LaneFiles, new InMemoryProcessLiveness(), new LaneBeats(_world.Values, _world.Delay), runner, _world.Log, _world.Clock));

        var result = await service.Handle(new LaneRecycleRequest("meta", Digest, DryRun: false, Detach: true), CancellationToken.None);

        Assert.Equal(MetaErrors.HostUnreadable.Code, result.Error.Code);
        Assert.Null((await _world.LaneFiles.Waiter("meta", CancellationToken.None)).Value);
        Assert.Empty(_world.Panes.Woken);
    }

    private sealed class FailingLauncher : IWaiterLauncher
    {
        public ValueTask<Kernel.Result<int>> Start(IReadOnlyList<string> argv, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Kernel.Result<int>>(MetaErrors.HostUnreadable.With("unavailable on Linux: setsid not found"));
    }
}
