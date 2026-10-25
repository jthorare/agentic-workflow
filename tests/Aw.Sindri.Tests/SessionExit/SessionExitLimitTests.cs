using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.SessionExit;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.SessionReports;

namespace Aw.Sindri.Tests.SessionExit;

/// <summary>'session wrap''s exit call when a sandbox limit ended the session (sandbox spec §3.3, §6): the session ends as 'limit' with the kind, the limit and the peaks, never as 'lost', and a final usage sample comes first.</summary>
[Lane(TestLane.Unit)]
public sealed class SessionExitLimitTests
{
    private const long Gib = 1024L * 1024 * 1024;
    private readonly SessionHarness _s = new();
    private RecordingUsageSampler? _usage;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SessionExitService Service()
    {
        _usage = new RecordingUsageSampler(_s);
        return new SessionExitService(_s.Events(), _usage);
    }

    [Fact]
    public async Task Exit_with_a_memory_limit_ends_the_session_as_limit_with_the_kind_the_limit_and_the_peaks()
    {
        await _s.Started();
        var peaks = new LimitPeaks(MemoryBytes: 5 * Gib, CpuPercent: 120, Tasks: 300, WallSeconds: 900);

        var result = await Service().Handle(A.SessionExitRequest().WithExitCode(137).WithLimit(new LimitEnd(LimitKind.Memory, 4 * Gib, 5 * Gib, peaks)).Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var ended = _s.Views.Slice.Events.Payload<SessionEnded>(Assert.Single(_s.Views.Slice.Store.Events)).Value;
        Expect.Equal((SessionEndReason.Limit, LimitKind.Memory, 4 * Gib, 5 * Gib), (ended.Reason, ended.Kind, ended.Limit, ended.Peak));
        Expect.Equal(peaks, ended.Peaks);
    }

    [Fact]
    public async Task Exit_with_a_wall_limit_ends_the_session_as_limit_never_as_lost_or_exit()
    {
        await _s.Started();

        _ = await Service().Handle(A.SessionExitRequest().WithExitCode(137).WithLimit(new LimitEnd(LimitKind.Wall, 15000, 15001)).Build(), Token);

        var ended = _s.Views.Slice.Events.Payload<SessionEnded>(Assert.Single(_s.Views.Slice.Store.Events)).Value;
        Expect.Equal((SessionEndReason.Limit, LimitKind.Wall), (ended.Reason, ended.Kind));
    }

    [Fact]
    public async Task Final_usage_sample_precedes_session_ended()
    {
        await _s.Started();
        var service = Service();

        _ = await service.Handle(A.SessionExitRequest().WithLimit(new LimitEnd(LimitKind.Wall, 15000, 15001)).Build(), Token);

        var flush = Assert.Single(_usage!.Flushes);
        Expect.Equal((A.FirstLaunchId, 0), (flush.Launch, flush.EventsBefore));   // asked while the ledger held no session.ended yet
        _ = Assert.Single(_s.Views.Slice.Store.Events);
    }

    [Fact]
    public async Task An_ordinary_exit_also_takes_the_final_usage_sample_first()
    {
        await _s.Started();
        var service = Service();

        _ = await service.Handle(A.SessionExitRequest().Build(), Token);

        Expect.Equal(0, Assert.Single(_usage!.Flushes).EventsBefore);
    }
}
