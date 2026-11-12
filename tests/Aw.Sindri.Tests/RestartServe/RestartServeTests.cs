using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.RestartServe;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;

namespace Aw.Sindri.Tests.RestartServe;

/// <summary>Restart serve onto the installed build (#3052): the relauncher is scheduled first, then the stop path runs; sessions, an equal build and a failed schedule change nothing.</summary>
public sealed class RestartServeTests
{
    private static readonly KeptRun _run = new("cursor/pid-333-100", "T-3/item/1", "build");

    private readonly SliceHarness _h = new();
    private readonly FakeRelauncher _relauncher;
    private readonly TemporalRuntime _runtime;

    public RestartServeTests()
    {
        _relauncher = new FakeRelauncher(_h.Shutdown);
        _runtime = new TemporalRuntime(new FakeTemporalServer(), TimeProvider.System);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private RestartServeService Service(IBuildStamps? stamps, params KeptRun[] open) =>
        new(_h.Callers, stamps, new FakeOpenRuns(open), _relauncher,
            new StopProcessService(_h.Held, _h.Store, _h.Store, _h.Events, _h.Shutdown, new ServeRecords(new InMemoryEndpointFile(), new HeldHumanToken(), new InMemoryHumanTokenStore()), _runtime));

    private IDisposable Human() => _h.Callers.Enter(A.Caller().Human().Build());

    [Fact, Trait("Lane", "unit")]
    public async Task Restart_schedules_the_relauncher_then_stops_and_lists_the_runs_it_keeps()
    {
        var grant = await _h.HoldLease();
        using var caller = Human();

        var result = await Service(new Stamps("old", "new"), _run).Handle(new RestartServeRequest(), Token);

        Assert.Equal(new RestartServeResponse(grant.Holder.Pid, "old", "new", [_run], "scheduled"), result.Value, new ResponseComparer());
        Assert.Equal(1, _relauncher.Calls);
        Assert.False(_relauncher.ShutdownAtSchedule);   // why: the helper must exist before serve stops, or nothing starts the new build
        Assert.True(_h.Shutdown.Requested);
        var stopped = Assert.Single(_h.Store.Events);
        Assert.Equal(EventTypes.ProcessStopped, stopped.Envelope.Type);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Restart_records_the_stop_with_the_reason_restart()
    {
        _ = await _h.HoldLease();
        using var caller = Human();

        _ = await Service(new Stamps("old", "new")).Handle(new RestartServeRequest(), Token);

        var stopped = Assert.Single(_h.Store.Events);
        Assert.Contains("\"reason\":\"restart\"", stopped.Envelope.Payload, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Restart_with_the_installed_build_already_running_answers_SND_BUILD_002_and_stops_nothing()
    {
        _ = await _h.HoldLease();
        using var caller = Human();

        var result = await Service(new Stamps("same", "same")).Handle(new RestartServeRequest(), Token);

        Assert.Equal(SindriErrors.BuildNothingNewer.Code, result.Error.Code);
        Assert.Equal(0, _relauncher.Calls);
        Assert.False(_h.Shutdown.Requested);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, "new")]
    [InlineData("old", null)]
    [InlineData(null, null)]
    public async Task Restart_in_a_dev_run_with_an_unknown_stamp_answers_SND_BUILD_002(string? running, string? installed)
    {
        _ = await _h.HoldLease();
        using var caller = Human();

        var result = await Service(new Stamps(running, installed)).Handle(new RestartServeRequest(), Token);

        Assert.Equal(SindriErrors.BuildNothingNewer.Code, result.Error.Code);
        Assert.Equal(0, _relauncher.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Restart_without_any_build_stamps_answers_SND_BUILD_002()
    {
        _ = await _h.HoldLease();
        using var caller = Human();

        var result = await Service(stamps: null).Handle(new RestartServeRequest(), Token);

        Assert.Equal(SindriErrors.BuildNothingNewer.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Restart_whose_relauncher_cannot_be_scheduled_answers_SND_BUILD_003_and_leaves_serve_running()
    {
        _ = await _h.HoldLease();
        using var caller = Human();
        _relauncher.Fails = true;

        var result = await Service(new Stamps("old", "new")).Handle(new RestartServeRequest(), Token);

        Assert.Equal(SindriErrors.BuildRelaunchFailed.Code, result.Error.Code);
        Assert.False(_h.Shutdown.Requested);
        Assert.NotNull(_h.Held.Current);
        Assert.Empty(_h.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Restart_by_a_session_caller_is_refused_SND_AUTH_002_before_anything_else()
    {
        _ = await _h.HoldLease();
        using var caller = _h.Callers.Enter(A.Caller().Session().Build());

        var result = await Service(new Stamps("old", "new"), _run).Handle(new RestartServeRequest(), Token);

        Assert.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
        Assert.Equal(0, _relauncher.Calls);
        Assert.False(_h.Shutdown.Requested);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Restart_without_a_caller_is_refused_SND_AUTH_002()
    {
        var result = await Service(new Stamps("old", "new")).Handle(new RestartServeRequest(), Token);

        Assert.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
    }

    private sealed record Stamps(string? Running, string? Installed) : IBuildStamps
    {
        string? IBuildStamps.Running() => Running;

        string? IBuildStamps.Installed() => Installed;
    }

    private sealed class FakeOpenRuns(IReadOnlyList<KeptRun> runs) : IOpenRuns
    {
        public ValueTask<IReadOnlyList<KeptRun>> Read(CancellationToken cancellationToken) => ValueTask.FromResult(runs);
    }

    private sealed class FakeRelauncher(FakeShutdown shutdown) : IServeRelauncher
    {
        public int Calls { get; private set; }

        public bool Fails { get; set; }

        public bool ShutdownAtSchedule { get; private set; }

        public Result<Unit> Schedule()
        {
            Calls++;
            ShutdownAtSchedule = shutdown.Requested;
            if (Fails)
            {
                return SindriErrors.BuildRelaunchFailed.With("no /bin/sh");
            }

            return Results.Ok();
        }
    }

    /// <summary>The response's run list is a reference list, so equality compares its items.</summary>
    private sealed class ResponseComparer : IEqualityComparer<RestartServeResponse>
    {
        public bool Equals(RestartServeResponse? x, RestartServeResponse? y) =>
            x is not null && y is not null && (x.Pid, x.FromBuild, x.ToBuild, x.Relaunch) == (y.Pid, y.FromBuild, y.ToBuild, y.Relaunch) && x.KeptRuns.SequenceEqual(y.KeptRuns);

        public int GetHashCode(RestartServeResponse obj) => obj.Pid;
    }
}
