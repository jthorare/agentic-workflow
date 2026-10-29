using System.Text.Json.Nodes;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Features.SyncSources;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Infrastructure.Linear;
using Aw.Context.Infrastructure.Sources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;
using Aw.Context.Tests.Onyx;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Linear;

/// <summary>The Linear project feed (projects page spec §4.1): the stored row each project becomes, the three phases, the catalog and the decision pass.</summary>
[Trait("Lane", "unit")]
public sealed class LinearProjectFeedTests
{
    private static readonly DateTimeOffset _at = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static (RecordFeed Feed, FakeLinearProjects Fake) FeedOf(IReadOnlyList<JsonObject> nodes, params string[] teams)
    {
        var fake = new FakeLinearProjects(nodes);
        var settings = LinearSettings.Create("http://127.0.0.1:1/graphql", "env:LINEAR_FIXTURE_KEY", teams.Length == 0 ? ["SPL"] : teams).Value;
        return (new LinearSources(Parts(fake)).CreateProjectFeed(settings), fake);
    }

    private static LinearSourceParts Parts(HttpMessageHandler fake) =>
        new(new HttpClient(fake), static _ => "fixture-key", new FakeTimeProvider(_at), new CapturingLogger<LinearClient>());

    private static async Task<RecordPage> Pull(RecordFeed feed, SourceCursor? after = null, DateTimeOffset? before = null, int limit = 50)
    {
        var page = await feed.Pull(new RecordPull(after, limit, NotBefore: _at.AddYears(1), before), TestContext.Current.CancellationToken);
        return page.Value;
    }

    private static async Task<RecordItem> One(Action<JsonObject> shape, params string[] teams)
    {
        var node = FakeLinearProjects.Node("p1", _at);
        shape(node);
        var (feed, _) = FeedOf([node], teams);
        return Assert.Single((await Pull(feed)).Items);
    }

    [Theory]
    [InlineData("backlog", RecordState.Open)]
    [InlineData("planned", RecordState.Open)]
    [InlineData("started", RecordState.Open)]
    [InlineData("paused", RecordState.Open)]
    [InlineData("completed", RecordState.Done)]
    [InlineData("canceled", RecordState.Canceled)]
    public async Task The_status_type_maps_to_a_record_state_and_is_kept_raw(string type, RecordState expected)
    {
        var item = await One(n => n["status"] = new JsonObject { ["name"] = "Renamed by the team", ["type"] = type });

        Assert.Equal((expected, type), (item.State, item.StateRaw));
    }

    [Theory]
    [InlineData("planned", RecordState.Open)]
    [InlineData("completed", RecordState.Done)]
    [InlineData("canceled", RecordState.Canceled)]
    public async Task Without_a_status_the_legacy_state_is_read(string state, RecordState expected)
    {
        var item = await One(n =>
        {
            _ = n.Remove("status");
            n["state"] = state;
        });

        Assert.Equal((expected, state), (item.State, item.StateRaw));
    }

    [Fact]
    public async Task A_full_project_carries_key_text_author_dates_version_health_due_and_the_lead_link()
    {
        var item = await One(n =>
        {
            n["status"] = new JsonObject { ["name"] = "Done", ["type"] = "completed" };
            n["completedAt"] = "2026-10-02T08:00:00Z";
            n["targetDate"] = "2026-11-15";
            n["lead"] = new JsonObject { ["id"] = "u1", ["name"] = "Ada" };
            n["lastUpdate"] = new JsonObject { ["health"] = "atRisk", ["createdAt"] = "2026-09-30T10:00:00Z" };
        });

        Assert.Equal(("linear:project/p1", RecordKind.Project, "Project p1", "Ada"), (item.Key.Value, item.Kind, item.Title, item.Author));
        Assert.Equal(new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.Zero), item.ClosedAt);
        Assert.Equal(("atRisk", new DateOnly(2026, 11, 15)), (item.Health, item.DueAt));
        Assert.Equal(new RecordLink("linear:user/u1", RecordLinkKind.LedBy, "Ada"), Assert.Single(item.Links));
        Assert.Contains("About p1", item.Text, StringComparison.Ordinal);
        Assert.Contains("Done", item.Text, StringComparison.Ordinal);
        Assert.Contains("2026-11-15", item.Text, StringComparison.Ordinal);
        Assert.Contains("atRisk", item.Text, StringComparison.Ordinal);
        Assert.Equal("linear:SPL", item.Audience?.Value);
    }

    [Fact]
    public async Task A_project_with_no_lead_no_update_and_no_target_has_no_link_health_or_due_date()
    {
        var item = await One(static _ => { });

        Assert.Equal((null, null, null, 0), (item.Author, item.Health, item.DueAt, item.Links.Count));
    }

    [Fact]
    public async Task The_audience_is_the_first_sorted_team_the_profile_lists_and_a_team_outside_the_profile_is_skipped()
    {
        var item = await One(n => n["teams"] = new JsonObject { ["nodes"] = new JsonArray(new JsonObject { ["key"] = "AAA" }, new JsonObject { ["key"] = "SPL" }) });

        Assert.Equal("linear:SPL", item.Audience?.Value);
    }

    [Fact]
    public async Task With_no_teams_in_the_profile_the_audience_is_the_first_sorted_team()
    {
        var node = FakeLinearProjects.Node("p1", _at);
        node["teams"] = new JsonObject { ["nodes"] = new JsonArray(new JsonObject { ["key"] = "ZED" }, new JsonObject { ["key"] = "AAA" }) };
        var fake = new FakeLinearProjects([node]);
        var settings = LinearSettings.Create("http://127.0.0.1:1/graphql", "env:LINEAR_FIXTURE_KEY", []).Value;
        var feed = new LinearSources(Parts(fake)).CreateProjectFeed(settings);

        var item = Assert.Single((await Pull(feed)).Items);

        Assert.Equal("linear:AAA", item.Audience?.Value);
    }

    [Fact]
    public async Task The_version_changes_when_only_the_last_updates_time_changes()
    {
        var first = await One(n => n["lastUpdate"] = new JsonObject { ["health"] = "onTrack", ["createdAt"] = "2026-09-30T10:00:00Z" });
        var second = await One(n => n["lastUpdate"] = new JsonObject { ["health"] = "onTrack", ["createdAt"] = "2026-09-30T11:00:00Z" });

        Assert.NotEqual(first.Version, second.Version);
    }

    [Fact]
    public async Task Recent_returns_every_project_in_scope_across_pages_whatever_the_window()
    {
        var nodes = Enumerable.Range(1, 5).Select(i => FakeLinearProjects.Node("p" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), _at.AddDays(-400 * i))).ToList();
        var (feed, _) = FeedOf(nodes);

        var first = await Pull(feed, limit: 2);
        var second = await Pull(feed, first.Next, limit: 2);
        var third = await Pull(feed, second.Next, limit: 2);

        Assert.Equal("2,2,1", string.Join(',', first.Items.Count, second.Items.Count, third.Items.Count));
    }

    [Fact]
    public async Task Backfill_returns_an_empty_page_at_once_without_calling_Linear()
    {
        var (feed, fake) = FeedOf([FakeLinearProjects.Node("p1", _at)]);

        var page = await Pull(feed, before: _at);

        Assert.Empty(page.Items);
        Assert.Empty(fake.Variables);
    }

    [Fact]
    public async Task Incremental_asks_for_projects_updated_after_the_watermark_in_the_profiles_teams()
    {
        var (feed, fake) = FeedOf([FakeLinearProjects.Node("p1", _at), FakeLinearProjects.Node("p2", _at.AddDays(1))]);
        var recent = await Pull(feed);

        var incremental = await Pull(feed, recent.Next);

        Assert.Empty(incremental.Items);
        var filter = fake.Variables[^1]["filter"]!;
        Assert.Equal(_at.AddDays(1).UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture), filter["updatedAt"]!["gt"]!.GetValue<string>());
        Assert.Equal("SPL", filter["accessibleTeams"]!["some"]!["key"]!["in"]![0]!.GetValue<string>());
    }

    [Fact]
    public void The_catalog_builds_the_project_feed_only_when_Linear_is_configured()
    {
        var parts = Parts(new FakeLinearProjects([]));
        var catalog = new ContextRecordFeedCatalog(new FakeRecordStore(), new GitCli(), new LinearSources(parts));
        var scrubbers = new FakeScrubberFactory().For([]).Value;
        var repos = MapWorld.Repos;
        var linear = LinearSettings.Create("http://127.0.0.1:1/graphql", "env:LINEAR_FIXTURE_KEY", ["SPL"]).Value;

        var with = catalog.For(new ContextSettings { Linear = linear }, repos, scrubbers).Select(static f => f.Source.Value).ToList();
        var without = catalog.For(new ContextSettings(), repos, scrubbers).Select(static f => f.Source.Value).ToList();

        Assert.Contains(LinearSourceIds.Projects.Value, with, StringComparer.Ordinal);
        Assert.DoesNotContain(LinearSourceIds.Projects.Value, without, StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_decision_pass_skips_the_project_feed_and_keeps_the_others()
    {
        using var world = new MapWorld();
        _ = await world.ImportFixture();
        var decisions = world.Service<FeedDecisions>();
        var scrubbers = new FakeScrubberFactory().For([]).Value;
        var context = (new ContextSettings(), scrubbers);

        var after = await decisions.For(MapWorld.Human, context, [], TestContext.Current.CancellationToken);

        Assert.Null(after(new FakeRecordFeed(LinearSourceIds.Projects, [])));
        Assert.NotNull(after(new FakeRecordFeed(LinearSourceIds.Issues, [])));
    }
}
