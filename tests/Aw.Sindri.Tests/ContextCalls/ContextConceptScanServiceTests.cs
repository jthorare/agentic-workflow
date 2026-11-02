using System.Text.Json;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The 'context-concept-scan' service (q110 C): it starts one scan job for each new map version, over the jobs view in memory.</summary>
public sealed class ContextConceptScanServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions _web = new(JsonSerializerDefaults.Web);
    private readonly SliceHarness _h = new();
    private readonly InMemoryJobViews _jobs = new();
    private readonly Starter _start;
    private readonly ScanEngineProxy _engine = ScanEngineProxy.Create();
    private Core.Lease.LeaseGrant? _grant;
    private int _next;

    public ContextConceptScanServiceTests()
    {
        _start = new Starter(_h);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<ContextConceptScanService> Service(bool context = true, bool approved = true)
    {
        _h.Clock.SetUtcNow(_now);
        var harness = approved ? _h : new SliceHarness();
        var loaded = SliceHarness.Profile("5s");
        loaded = loaded with { Profile = loaded.Profile with { Context = context ? JsonDocument.Parse("""{"docs":{"globs":["docs/**/*.md"]}}""").RootElement.Clone() : null } };
        if (approved)
        {
            _ = await _h.Profiles.Save(loaded, Ct);
        }

        return new ContextConceptScanService(new ContextScopes(harness.Profiles, _h.Callers), _engine.Proxy, _jobs, _start, _h.Clock, NullLogger<ContextConceptScanService>.Instance);
    }

    private void Scan(JobState state, TimeSpan endedAgo, int version = 7)
    {
        _next++;
        var result = state == JobState.Succeeded ? JsonSerializer.SerializeToElement(new ScanContextConceptsResult(version, OnDraft: true, "scanned", Hints: 1, Searches: 2, Reason: null), _web) : (JsonElement?)null;
        _jobs.Put(new JobRow(JobId.New(_h.Ids), ContextConceptScanService.Kind, JobKey.From("concept-scan"), Item: null, Repo: null, Service: null, state,
            "service:context-concept-scan", _now - endedAgo - TimeSpan.FromSeconds(30), state == JobState.Running ? null : _now - endedAgo, [], Rounds: 0, Summary: null,
            state == JobState.Failed ? "SND-CONTEXT-004" : null, result, CancelledBy: null, Sequence: _next));
    }

    private async Task<Result<ScheduledResult>> Run(ContextConceptScanService service)
    {
        _grant ??= await _h.HoldLease();
        using var caller = _h.Callers.EnterService("context-concept-scan");
        return await service.Run(new ScheduledRun(_grant, _now), Ct);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_new_draft_starts_one_scan_as_the_service()
    {
        _engine.State = new MapPublishState(Hold: null, LatestDraft: 7, Current: 6, CurrentHeads: []);

        var result = await Run(await Service());

        Assert.False(result.Value.Idle);
        Assert.Contains("map v7", result.Value.Summary, StringComparison.Ordinal);
        _ = Assert.Single(_start.Requests);
        Assert.Equal("service:context-concept-scan", _start.Actors.Single());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_draft_the_current_version_is_scanned()
    {
        _engine.State = new MapPublishState(Hold: null, LatestDraft: null, Current: 6, CurrentHeads: []);

        var result = await Run(await Service());

        Assert.Contains("map v6", result.Value.Summary, StringComparison.Ordinal);
        _ = Assert.Single(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_version_that_was_scanned_is_not_scanned_again()
    {
        _engine.State = new MapPublishState(Hold: null, LatestDraft: 7, Current: 6, CurrentHeads: []);
        Scan(JobState.Succeeded, TimeSpan.FromMinutes(30), version: 7);

        var result = await Run(await Service());

        Assert.True(result.Value.Idle);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_next_version_is_scanned_even_when_an_earlier_one_was()
    {
        _engine.State = new MapPublishState(Hold: null, LatestDraft: 8, Current: 7, CurrentHeads: []);
        Scan(JobState.Succeeded, TimeSpan.FromMinutes(30), version: 7);

        _ = await Run(await Service());

        _ = Assert.Single(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_running_scan_holds_the_next_one_back()
    {
        _engine.State = new MapPublishState(Hold: null, LatestDraft: 8, Current: 7, CurrentHeads: []);
        Scan(JobState.Running, TimeSpan.Zero, version: 7);

        var result = await Run(await Service());

        Assert.True(result.Value.Idle);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_scan_that_failed_three_minutes_ago_makes_the_service_wait()
    {
        _engine.State = new MapPublishState(Hold: null, LatestDraft: 7, Current: 6, CurrentHeads: []);
        Scan(JobState.Failed, TimeSpan.FromMinutes(3));

        var result = await Run(await Service());

        Assert.True(result.Value.Idle);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_scan_that_failed_twenty_minutes_ago_no_longer_holds_it_back()
    {
        _engine.State = new MapPublishState(Hold: null, LatestDraft: 7, Current: 6, CurrentHeads: []);
        Scan(JobState.Failed, TimeSpan.FromMinutes(20));

        _ = await Run(await Service());

        _ = Assert.Single(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_map_yet_the_tick_is_quiet()
    {
        var result = await Run(await Service());

        Assert.True(result.Value.Idle);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_approved_profile_the_tick_is_quiet_not_failed()
    {
        var result = await Run(await Service(approved: false));

        Assert.True(result.Value.Idle);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_conflict_from_the_start_is_quiet()
    {
        _engine.State = new MapPublishState(Hold: null, LatestDraft: 7, Current: 6, CurrentHeads: []);
        _start.Answer = SindriErrors.JobConflict.With("a scan is running");

        var result = await Run(await Service());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Idle);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Any_other_refusal_to_start_fails_the_run()
    {
        _engine.State = new MapPublishState(Hold: null, LatestDraft: 7, Current: 6, CurrentHeads: []);
        _start.Answer = SindriErrors.InvalidInput.With("no");

        var result = await Run(await Service());

        Assert.True(result.IsError);
    }

    private sealed class Starter(SliceHarness harness) : IService<ScanContextConceptsRequest, JobStartedResponse>
    {
        internal List<ScanContextConceptsRequest> Requests { get; } = [];

        internal List<string> Actors { get; } = [];

        internal Error? Answer { get; set; }

        public ValueTask<Result<JobStartedResponse>> Handle(ScanContextConceptsRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Actors.Add(harness.Callers.Current?.Actor ?? string.Empty);
            return ValueTask.FromResult<Result<JobStartedResponse>>(Answer is { } error
                ? error
                : new JobStartedResponse(new CommandAccepted(harness.Ids.New<CommandId>(), 1), JobId.New(harness.Ids), new JobLinks("/j", "/c", "/e")));
        }
    }
}
