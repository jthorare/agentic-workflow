using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Kernel;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Tests.Workflow.Blocks.Input;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The 'index-sync' service (owned-index spec §5.5) over a fake <see cref="IIndexSync"/> behind the engine facade, and the milestones the progress rows carry (§5.8).</summary>
public sealed class IndexSyncServiceTests
{
    private const string OnyxSection = """{"sources":{"onyx":{"url":"http://127.0.0.1:3189","token":"on_live-key-0123","version":"v4.9.0"}}}""";
    private static readonly DateTimeOffset _now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private readonly SliceHarness _h = new();
    private readonly FakeIndexSync _index = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Result<ScheduledResult>> Run(bool context)
    {
        _h.Clock.SetUtcNow(_now);
        var loaded = SliceHarness.Profile("5m");
        loaded = loaded with { Profile = loaded.Profile with { Context = context ? JsonDocument.Parse(OnyxSection).RootElement.Clone() : null } };
        _ = await _h.Profiles.Save(loaded, Ct);
        var grant = await _h.HoldLease();
        using var caller = _h.Callers.EnterService("index-sync");
        return await new IndexSyncService(new ContextScopes(_h.Profiles, _h.Callers), EngineProxy.Of(_index)).Run(new ScheduledRun(grant, _now), Ct);
    }

    private static SourceSync Row(string source, int fetched, string? code = null) =>
        new(SourceId.From(source), Records: fetched, fetched, Unchanged: 0, Linked: 0, Unlinked: 0, Orphaned: 0, Decisions: 0, Queued: 0, Refused: 0, Tokens: 0, code);

    [Fact, Trait("Lane", "unit")]
    public void IndexSync_is_a_five_minute_built_in()
    {
        Assert.Equal("index-sync", IndexSyncService.BuiltIn.Name.Value);
        Assert.Equal(TimeSpan.FromMinutes(5), IndexSyncService.BuiltIn.Cadence.Interval);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Tick_runs_the_feeds_once_and_reports_each_source()
    {
        _index.Rows = [Row("linear", 12), Row("github", 3)];

        var result = await Run(context: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _index.Ticks);
        Assert.False(result.Value.Idle);
        Assert.Equal("ok: linear 12 fetched, github 3 fetched", result.Value.Summary);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Tick_maps_a_down_source_to_a_row_with_its_code_not_a_failed_run()
    {
        _index.Rows = [Row("linear", 0, ContextErrors.OnyxUnexpectedReply.Code), Row("github", 3)];

        var result = await Run(context: true);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Idle);
        Assert.Equal($"ok: linear {ContextErrors.OnyxUnexpectedReply.Code}, github 3 fetched", result.Value.Summary);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Tick_with_nothing_new_is_quiet()
    {
        _index.Rows = [Row("linear", 0)];

        var result = await Run(context: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Idle);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Tick_without_a_context_section_does_not_call_the_sync()
    {
        var result = await Run(context: false);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Idle);
        Assert.Equal(0, _index.Ticks);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, false, false)]
    [InlineData(SyncPhase.Recent, false, false)]
    [InlineData(SyncPhase.Backfill, true, false)]
    [InlineData(SyncPhase.Incremental, true, true)]
    [InlineData(SyncPhase.Reconcile, true, true)]
    public void Milestones_follow_the_stored_phase(SyncPhase? phase, bool usable, bool complete)
    {
        var progress = SourceProgress.From(SourceId.From("linear"), phase, _now, "SND-CONTEXT-010");

        Assert.Equal(usable, progress.Usable);
        Assert.Equal(complete, progress.Complete);
        Assert.Equal("SND-CONTEXT-010", progress.LastCode);
        Assert.Equal(_now, progress.LastSync);
    }

    private sealed class FakeIndexSync : IIndexSync
    {
        public IReadOnlyList<SourceSync> Rows { get; set; } = [];

        public int Ticks { get; private set; }

        public ValueTask<Result<SyncReport>> Tick(ContextScope scope, CancellationToken cancellationToken)
        {
            Ticks++;
            return ValueTask.FromResult<Result<SyncReport>>(new SyncReport(Rows));
        }

        public ValueTask<Result<IReadOnlyList<SourceProgress>>> Progress(ContextScope scope, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<IReadOnlyList<SourceProgress>>>(Results.Success<IReadOnlyList<SourceProgress>>([]));
    }
}
