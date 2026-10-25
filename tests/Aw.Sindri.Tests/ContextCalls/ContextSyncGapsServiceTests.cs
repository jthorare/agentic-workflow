using System.Text.Json;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The 'context-sync-gaps' service (scoping spec C5, owner decision 2026-10-18) and the 'sync' it leaves on a not-synced row, over the jobs view in memory.</summary>
public sealed class ContextSyncGapsServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private readonly SliceHarness _h = new();
    private readonly InMemoryJobViews _jobs = new();
    private readonly Starter _start;
    private int _next;
    private Core.Lease.LeaseGrant? _grant;

    public ContextSyncGapsServiceTests()
    {
        _start = new Starter(_h);
        _counted = new CountingJobViews(_jobs);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string OnyxSection = """{"sources":{"onyx":{"url":"http://127.0.0.1:3189","token":"on_live-key-0123","version":"v4.9.0"}}}""";
    private readonly GapEngineProxy _engine = GapEngineProxy.Create();
    private readonly CountingJobViews _counted;
    private readonly CapturingLogger _log = new();

    private async Task<ContextSyncGapsService> Service(bool context)
    {
        _h.Clock.SetUtcNow(_now);
        var loaded = SliceHarness.Profile("5s");
        loaded = loaded with { Profile = loaded.Profile with { Context = context ? JsonDocument.Parse(OnyxSection).RootElement.Clone() : null } };
        _ = await _h.Profiles.Save(loaded, Ct);
        return new ContextSyncGapsService(new ContextScopes(_h.Profiles, _h.Callers), _engine.Proxy, _counted, _start, _h.Clock, _log);
    }

    private void Synced() => _engine.Gap = false;

    private void FailedFirstSync() => _engine.Gap = true;   // a failed first sync is still a gap: the engine's stored-state check says so (SyncGapTests)

    private void Job(JobState state, TimeSpan endedAgo)
    {
        _next++;
        _jobs.Put(new JobRow(JobId.New(_h.Ids), SyncGaps.SyncKind, JobKey.From("sync"), Item: null, Repo: null, Service: null, state,
            "human:cli", _now - endedAgo - TimeSpan.FromSeconds(5), state == JobState.Running ? null : _now - endedAgo, [], Rounds: 0, Summary: null, state == JobState.Failed ? "SND-CONTEXT-030" : null, Result: null, CancelledBy: null, Sequence: _next));
    }

    private async Task<Result<ScheduledResult>> Run(ContextSyncGapsService service)
    {
        _grant ??= await _h.HoldLease();
        var grant = _grant;
        using var caller = _h.Callers.EnterService("context-sync-gaps");
        return await service.Run(new ScheduledRun(grant, _now), Ct);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Never_synced_source_starts_one_sync_of_every_source()
    {
        var service = await Service(context: true);

        var result = await Run(service);

        Assert.False(result.Value.Idle);
        Assert.Null(Assert.Single(_start.Requests).Source);
        Assert.Equal("service:context-sync-gaps", _start.Actors.Single());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Nothing_to_do_without_a_context_section_a_synced_source_or_a_failed_attempt()
    {
        Assert.True((await Run(await Service(context: false))).Value.Idle);
        Synced();
        Assert.True((await Run(await Service(context: true))).Value.Idle);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_quiet_tick_asks_the_stored_state_check_once_and_reads_no_job_view()
    {
        Synced();
        var service = await Service(context: true);

        var result = await Run(service);

        Assert.True(result.Value.Idle);
        Assert.Equal(1, _engine.Calls);   // the proxy refuses every other engine call, so no live probe
        Assert.Equal(0, _counted.Lists);
        Assert.Empty(_log.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_first_sync_that_failed_is_still_a_gap_and_is_retried_on_the_backoff_not_before()
    {
        FailedFirstSync();
        Job(JobState.Failed, TimeSpan.FromMinutes(3));   // ended 3 minutes ago: the first wait is 5
        var service = await Service(context: true);
        var waiting = await Run(service);
        _h.Clock.Advance(TimeSpan.FromMinutes(3));
        var elapsed = await Run(service);

        Assert.True(waiting.Value.Idle);
        Assert.False(elapsed.Value.Idle);
        _ = Assert.Single(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_first_sync_that_failed_with_no_job_on_record_is_started()
    {
        FailedFirstSync();

        var result = await Run(await Service(context: true));

        Assert.False(result.Value.Idle);
        _ = Assert.Single(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Only_the_newest_runs_are_taken_for_the_failure_count()
    {
        for (var i = 0; i < 40; i++)
        {
            Job(JobState.Failed, TimeSpan.FromHours(2) + TimeSpan.FromMinutes(i));
        }

        var result = await Run(await Service(context: true));   // far more failures than the 60 minute cap needs; the last one ended 2 hours ago

        Assert.False(result.Value.Idle);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_tick_with_no_approved_profile_is_quiet_not_a_failed_run()
    {
        var service = new ContextSyncGapsService(new ContextScopes(_h.Profiles, _h.Callers), _engine.Proxy, _counted, _start, _h.Clock, _log);

        var result = await Run(service);

        Assert.True(result.Value.Idle);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_running_sync_is_never_doubled()
    {
        Job(JobState.Running, TimeSpan.Zero);
        var service = await Service(context: true);

        var result = await Run(service);

        Assert.True(result.Value.Idle);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_sync_backs_off_and_each_failure_in_a_row_doubles_the_wait()
    {
        Job(JobState.Failed, TimeSpan.FromMinutes(3));   // newest: ended 3 minutes ago, wait 5
        var service = await Service(context: true);
        var waiting = await Run(service);
        _h.Clock.Advance(TimeSpan.FromMinutes(3));   // 6 minutes after it ended
        var elapsed = await Run(service);

        Assert.True(waiting.Value.Idle);
        Assert.False(elapsed.Value.Idle);
        _ = Assert.Single(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_sync_that_ended_Succeeded_but_left_the_source_unsynced_counts_as_a_failed_attempt_and_the_wait_doubles()
    {
        // the source is down: the job ends Succeeded with a coded row, and the gap is still there, so each run counts
        Job(JobState.Succeeded, TimeSpan.FromMinutes(20));
        Job(JobState.Succeeded, TimeSpan.FromMinutes(7));   // newest: the second wait is 10 minutes from 7 minutes ago
        var service = await Service(context: true);
        var waiting = await Run(service);
        _h.Clock.Advance(TimeSpan.FromMinutes(4));   // 11 minutes after it ended
        var elapsed = await Run(service);

        Assert.True(waiting.Value.Idle);
        Assert.False(elapsed.Value.Idle);
        _ = Assert.Single(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_down_source_does_not_start_a_sync_every_minute()
    {
        Job(JobState.Succeeded, TimeSpan.FromMinutes(1));
        var service = await Service(context: true);

        var result = await Run(service);

        Assert.True(result.Value.Idle);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_same_sync_already_running_is_quiet_any_other_409_is_quiet_but_logged_and_any_other_refusal_fails()
    {
        var service = await Service(context: true);
        _start.Answer = Running("JOB-0000000000000001");
        var same = await Run(service);
        Assert.Empty(_log.Lines);
        _start.Answer = SindriErrors.JobConflict.With("The job already ended");
        var other = await Run(service);
        _start.Answer = SindriErrors.JobsRateLimited.With("too many jobs");
        var refused = await Run(service);

        Assert.True(same.Value.Idle);
        Assert.True(other.Value.Idle);
        Assert.Contains(_log.Lines, l => l.Contains("SND-JOB-409", StringComparison.Ordinal));
        Assert.Equal(SindriErrors.JobsRateLimited.Code, refused.Error.Code);
    }

    private static Error Running(string job)
    {
        using var id = JsonDocument.Parse($"\"{job}\"");
        return SindriErrors.JobConflict.With("A sync job is already running for sync").WithExtension("runningJob", id.RootElement.Clone());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_running_sync_is_named_on_a_not_synced_row_only()
    {
        Job(JobState.Running, TimeSpan.Zero);
        var gaps = new SyncGaps(_jobs);
        var notSynced = new SearchedSource(SourceId.From("onyx"), SearchedState.Unavailable, "not-synced", "SND-CONTEXT-161", AsOf: null);
        var ok = new SearchedSource(SourceId.From("git-history"), SearchedState.Ok, Reason: null, Code: null, _now);

        var marked = await gaps.Mark(new LensSearched(["a"], "v1", 0, [], [notSynced, ok], new SearchedBands(new BandSearched(Empty: true), new BandSearched(Empty: true), new BandSearched(Empty: true))), Ct);

        Assert.Equal("running", marked.Sources[0].Sync!.State);
        Assert.StartsWith("JOB-", marked.Sources[0].Sync!.JobId.Value, StringComparison.Ordinal);
        Assert.Null(marked.Sources[1].Sync);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_gap_with_no_job_running_carries_no_sync()
    {
        Job(JobState.Succeeded, TimeSpan.FromMinutes(1));
        var notSynced = new SearchedSource(SourceId.From("onyx"), SearchedState.Unavailable, "not-synced", "SND-CONTEXT-161", AsOf: null);

        var marked = await new SyncGaps(_jobs).Mark(new LensSearched(["a"], "v1", 0, [], [notSynced], new SearchedBands(new BandSearched(Empty: true), new BandSearched(Empty: true), new BandSearched(Empty: true))), Ct);

        Assert.Null(marked.Sources[0].Sync);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_service_is_a_built_in_that_may_start_a_job()
    {
        Assert.Equal("context-sync-gaps", ContextSyncGapsService.BuiltIn.Name.Value);
        var row = Commands.Authorization.Rows.Of(ActionClass.Job, "SyncSources");
        Assert.Null(Authorization.Decide(ActionClass.Job, CallerContext.Service("context-sync-gaps", _h.Ids), row).Refusal);
    }

    private sealed class CountingJobViews(InMemoryJobViews inner) : IJobViews
    {
        internal int Lists { get; private set; }

        public ValueTask<Result<JobRow?>> Get(JobId job, CancellationToken cancellationToken) => inner.Get(job, cancellationToken);

        public ValueTask<Result<IReadOnlyList<JobRow>>> Running(CancellationToken cancellationToken) => inner.Running(cancellationToken);

        public ValueTask<Result<IReadOnlyList<JobRow>>> List(JobState? state, JobKind? kind, CancellationToken cancellationToken)
        {
            Lists++;
            return inner.List(state, kind, cancellationToken);
        }
    }

    private sealed class CapturingLogger : ILogger<ContextSyncGapsService>
    {
        internal List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception));
    }

    private sealed class Starter(SliceHarness harness) : IService<SyncContextSourcesRequest, JobStartedResponse>
    {
        internal List<SyncContextSourcesRequest> Requests { get; } = [];

        internal List<string> Actors { get; } = [];

        internal Error? Answer { get; set; }

        public ValueTask<Result<JobStartedResponse>> Handle(SyncContextSourcesRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Actors.Add(harness.Callers.Current?.Actor ?? string.Empty);
            return ValueTask.FromResult<Result<JobStartedResponse>>(Answer is { } error
                ? error
                : new JobStartedResponse(new CommandAccepted(harness.Ids.New<CommandId>(), 1), JobId.New(harness.Ids), new JobLinks("/j", "/c", "/e")));
        }
    }
}
