using Aw.Context.Core;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Features.SyncSources;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;
using Aw.Kernel;

namespace Aw.Context.Tests.SyncSources;

/// <summary>
/// The index tick of the Linear issue feed also reads the pickers' lists, after the issues and even when the tick ended at its time cap (linear hub spec 2026-11-04 §4.2):
/// states, cycles, members and labels of each team, replacing the team's rows.
/// </summary>
[Trait("Lane", "unit")]
public sealed class TickLookupsTests
{
    private const string Linear = """{"sources":{"linear":{"teams":["ENG"],"token":"env:LINEAR_KEY"}}}""";
    private static readonly SourceId _issues = LinearSourceIds.Issues;
    private static readonly ExternalId _eng = ExternalId.From("team-eng");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RecordItem Item() => new(
        RecordKey.From("linear:issue/i1"), RecordKind.Issue, "Title", "Words of the issue", Author: null, Url: null, RecordState.Open, StateRaw: null,
        CreatedAt: null, RecordSamples.Now.AddDays(-1), ClosedAt: null, SourceVersion.From("v1"), Links: [], Repo: null, Audience: AudienceKey.From("linear:team-a"));

    private static LinearTeamLookups Team(string state) => new(
        _eng, "ENG",
        [new LinearLookupState(ExternalId.From("state-1"), state, "started", 0)],
        [new LinearLookupCycle(ExternalId.From("cycle-1"), 1, "C1", RecordSamples.Now, RecordSamples.Now.AddDays(14))],
        [new LinearLookupMember(ExternalId.From("user-1"), "Ada", DisplayName: null, Active: true)],
        [new LinearLookupLabel(ExternalId.From("label-1"), "bug", "#eb5757")],
        "Engineering",
        RecordSamples.Now);

    private static MapWorld Rig(RecordFeed? feed = null, SourceId? source = null) => new(feeds: new FakeRecordFeedCatalog(feed ?? new FakeRecordFeed(source ?? _issues, [Item()])));

    private static ValueTask<Result<SyncReport>> Tick(MapWorld world, string settings = Linear) => world.Engine.Tick(MapWorld.HumanWith(settings), Ct);

    [Fact]
    public async Task A_tick_of_the_issue_feed_stores_each_teams_lists()
    {
        using var world = Rig();
        world.Lookups.Answer = new[] { Team("In Progress") };

        var report = await Tick(world);

        Assert.True(report.IsSuccess, report.IsError ? report.Error.Message : null);
        Assert.Equal(["In Progress"], world.LookupStore.Team(_eng)?.States.Select(static s => s.Name), StringComparer.Ordinal);
        Assert.Equal(1, world.Lookups.Calls);
    }

    [Fact]
    public async Task With_no_teams_named_an_unsynced_key_reads_as_null()
    {
        using var world = Rig();

        var read = await world.Engine.IssueLookups("ENGG", MapWorld.HumanWith("""{"sources":{"linear":{"token":"env:LINEAR_KEY"}}}"""), Ct);

        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : null);
        Assert.Null(read.Value);
    }

    [Fact]
    public async Task A_team_is_stamped_with_the_time_of_its_replace()
    {
        using var world = Rig();
        world.Lookups.Answer = new[] { Team("In Progress") with { SyncedAt = default } };

        _ = await Tick(world);

        Assert.Equal(world.Clock.GetUtcNow(), world.LookupStore.Team(_eng)?.SyncedAt);
    }

    [Fact]
    public async Task A_tick_stores_the_workspace_url_key()
    {
        using var world = Rig();
        world.Lookups.UrlKeyAnswer = Results.Success<string?>("acme");

        _ = await Tick(world);

        Assert.Equal("acme", (await world.LookupStore.WorkspaceUrlKey(Ct)).Value);
    }

    [Fact]
    public async Task The_lists_are_read_after_the_issues_are_stored()
    {
        using var world = Rig();
        var recordsWhenAsked = -1;
        world.Lookups.OnFetch = () => recordsWhenAsked = world.Records.Count(_issues, Ct).AsTask().GetAwaiter().GetResult().Value.Records;

        _ = await Tick(world);

        Assert.Equal(1, recordsWhenAsked);
    }

    [Fact]
    public async Task A_second_tick_replaces_the_teams_rows()
    {
        using var world = Rig();
        world.Lookups.Answer = new[] { Team("One") };
        _ = await Tick(world);
        world.Lookups.Answer = new[] { Team("Two") };

        _ = await Tick(world);

        Assert.Equal(["Two"], world.LookupStore.Team(_eng)?.States.Select(static s => s.Name), StringComparer.Ordinal);
        Assert.Equal(2, world.LookupStore.Replaces);
    }

    [Fact]
    public async Task A_tick_that_ended_at_its_time_cap_still_fills_the_lists()
    {
        var slow = new SlowFirstPage(new FakeRecordFeed(_issues, [Item()]));
        using var world = Rig(slow);
        slow.Takes = () => world.Clock.Advance(SyncSourcesService.RunCap + TimeSpan.FromSeconds(1));
        world.Lookups.Answer = new[] { Team("In Progress") };

        var report = await Tick(world);

        var row = Assert.Single(report.Value.Sources);
        Assert.Equal(ContextErrors.SyncCapped.Code, row.Code);
        Assert.Equal(["In Progress"], world.LookupStore.Team(_eng)?.States.Select(static s => s.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_tick_that_failed_reads_no_lists()
    {
        using var world = Rig(new DownFeed(_issues));

        _ = await Tick(world);

        Assert.Equal(0, world.Lookups.Calls);
        Assert.Null(world.LookupStore.Team(_eng));
    }

    [Fact]
    public async Task The_step_runs_only_for_the_issue_feed()
    {
        using var world = Rig(source: LinearSourceIds.Projects);

        _ = await Tick(world);

        Assert.Equal(0, world.Lookups.Calls);
    }

    [Fact]
    public async Task The_step_does_not_run_when_linear_is_not_configured()
    {
        using var world = Rig();

        _ = await Tick(world, """{"sources":{}}""");

        Assert.Equal(0, world.Lookups.Calls);
    }

    [Fact]
    public async Task A_failed_read_leaves_the_stored_rows_and_the_feeds_result_ok()
    {
        using var world = Rig();
        world.Lookups.Answer = new[] { Team("In Progress") };
        _ = await Tick(world);
        world.Lookups.Answer = ContextErrors.LinearKeyRefused.With("revoked");

        var report = await Tick(world);

        Assert.True(report.IsSuccess, report.IsError ? report.Error.Message : null);
        Assert.Null(Assert.Single(report.Value.Sources).Code);
        Assert.Equal(["In Progress"], world.LookupStore.Team(_eng)?.States.Select(static s => s.Name), StringComparer.Ordinal);
    }

    /// <summary>A feed whose first page takes longer than a run may.</summary>
    private sealed class SlowFirstPage(RecordFeed inner) : RecordFeed(inner.Source)
    {
        public Action Takes { get; set; } = static () => { };

        public override ValueTask<Result<RecordPage>> Pull(RecordPull pull, CancellationToken cancellationToken)
        {
            Takes();
            return inner.Pull(pull, cancellationToken);
        }
    }

    /// <summary>A feed whose source is down.</summary>
    private sealed class DownFeed(SourceId source) : RecordFeed(source)
    {
        public override ValueTask<Result<RecordPage>> Pull(RecordPull pull, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<RecordPage>>(ContextErrors.SourceUnavailable.With("down"));
    }
}
