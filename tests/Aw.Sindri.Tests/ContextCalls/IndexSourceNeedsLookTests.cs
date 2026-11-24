using Aw.Context.Core.Map;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Each context source row carries needsLook, decided on the server (issue #3430): a failing pull or a source that holds nothing and never committed after a pass ended is a look; a running or new pull is not.</summary>
public sealed class IndexSourceNeedsLookTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, SourceProgress> _sources = new(StringComparer.Ordinal)
    {
        ["failed pull"] = Source() with { Pull = "failed", Records = 40, LastCommit = _now.AddHours(-2) },
        ["pull in backoff"] = Source() with { Pull = "backoff", Records = 40, LastCommit = _now.AddHours(-2) },
        ["idle with nothing ever committed"] = Source() with { Pull = "idle", Records = 0 },
        ["idle with records"] = Source() with { Pull = "idle", Records = 40, LastCommit = _now.AddHours(-2) },
        ["idle, no pass ended yet"] = Source() with { Pull = "idle", Records = 0, LastSync = null },
        ["idle, empty but committed"] = Source() with { Pull = "idle", Records = 0, LastCommit = _now.AddHours(-2) },
        ["running first read"] = Source() with { Pull = "running", Records = 0 },
        ["a pull word the app never saw"] = Source() with { Pull = "throttled", Records = 0 },
        ["unread pull, unusable with a code"] = Source(usable: false, lastCode: "SND-CONTEXT-010"),
        ["unread pull, unusable with no code"] = Source(usable: false, lastCode: null),
        ["unread pull, usable with a code"] = Source(usable: true, lastCode: "SND-CONTEXT-059"),
    };

    private readonly SliceHarness _h = new();
    private readonly FakeIndexSync _index = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string, bool> Cases => new()
    {
        { "failed pull", true },
        { "pull in backoff", true },
        { "idle with nothing ever committed", true },
        { "idle with records", false },
        { "idle, no pass ended yet", false },
        { "idle, empty but committed", false },
        { "running first read", false },
        { "a pull word the app never saw", false },
        { "unread pull, unusable with a code", true },
        { "unread pull, unusable with no code", false },
        { "unread pull, usable with a code", false },
    };

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Cases))]
    public async Task The_status_row_says_whether_the_source_needs_a_look(string name, bool look)
    {
        var row = await Status(_sources[name], new SourceSyncClock(TimeProvider.System), new NoPauses());

        Assert.Equal(look, row.NeedsLook);
    }

    public static TheoryData<int, bool, string?> Limit => new()
    {
        { 14, false, null },
        { 15, true, "linear hasn't finished a sync in 15 minutes" },
    };

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Limit))]
    public async Task A_source_with_no_finished_pass_needs_no_look_at_14_minutes_and_a_look_at_15_3909(int minutes, bool look, string? words)
    {
        var row = await Status(NeverSynced(), Started(TimeSpan.FromMinutes(minutes)), new NoPauses());

        Assert.Equal((look, words), (row.NeedsLook, row.LookWords));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_paused_index_sync_service_never_trips_the_first_sync_limit_3909()
    {
        var row = await Status(NeverSynced(), Started(TimeSpan.FromMinutes(40)), new PausedServices(IndexSyncService.ServiceId));

        Assert.False(row.NeedsLook);
        Assert.Null(row.LookWords);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_is_not_flagged_right_after_index_sync_resumes_from_a_long_pause_3909()
    {
        var row = await Status(NeverSynced(), Started(TimeSpan.FromMinutes(40)), new ResumedService(IndexSyncService.ServiceId, _now.AddMinutes(39)));

        Assert.False(row.NeedsLook);
        Assert.Null(row.LookWords);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_is_flagged_16_minutes_after_index_sync_resumes_3909()
    {
        var row = await Status(NeverSynced(), Started(TimeSpan.FromMinutes(56)), new ResumedService(IndexSyncService.ServiceId, _now.AddMinutes(40)));

        Assert.Equal((true, "linear hasn't finished a sync in 15 minutes"), (row.NeedsLook, row.LookWords));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_resume_before_serve_started_leaves_the_count_at_serve_start_3909()
    {
        var row = await Status(NeverSynced(), Started(TimeSpan.FromMinutes(14)), new ResumedService(IndexSyncService.ServiceId, _now.AddHours(-3)));

        Assert.False(row.NeedsLook);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_first_pass_under_a_30_minute_index_sync_that_committed_20_minutes_ago_needs_no_look_4035()
    {
        var backfilling = Source() with { Phase = SyncPhase.Backfill, Complete = false, Pull = "idle", Records = 12_400, LastSync = _now.AddMinutes(20), LastCommit = _now.AddMinutes(20) };

        var row = await Status(backfilling, Started(TimeSpan.FromMinutes(40)), new NoPauses(), SliceHarness.ProfileOf("thor", ("index-sync", "30m")));

        Assert.False(row.NeedsLook);
    }

    private static SourceProgress NeverSynced() => Source() with { LastSync = null, Pull = "running", Records = 0 };

    private static SourceSyncClock Started(TimeSpan ago)
    {
        var clock = new FakeTimeProvider(_now);
        var started = new SourceSyncClock(clock);
        clock.Advance(ago);
        return started;
    }

    private async Task<IndexSourceRow> Status(SourceProgress source, SourceSyncClock started, IServicePauses pauses, LoadedProfile? profile = null)
    {
        _ = await _h.Profiles.Save(profile ?? SliceHarness.Profile("5m"), Ct);
        _index.Standing = [source];
        using var caller = _h.Callers.EnterService("test");

        var view = await new GetIndexStatusService(new ContextScopes(_h.Profiles, _h.Callers, new FakeRepoCheckouts()), IndexStoreEngineProxy.Create(_index, new MapHealth("context:store", MapHealthState.Ok, "ok", Fix: null)), started, pauses)
            .Handle(new GetIndexStatusRequest(), Ct);

        Assert.True(view.IsSuccess);
        return Assert.Single(view.Value.Sources);
    }

    private static SourceProgress Source(bool usable = true, string? lastCode = null) =>
        new(SourceId.From("linear"), usable ? SyncPhase.Incremental : SyncPhase.Recent, _now.AddMinutes(-5), lastCode, Usable: usable, Complete: usable);
}
