using System.Text.Json.Nodes;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Features.SyncSources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Linear;

/// <summary>An issue's project as a link, the feed-format version that makes stored issues re-save once, and the first read that also takes old open issues of open projects (projects page spec 2026-10-29 §4.3, §4.4).</summary>
[Trait("Lane", "unit")]
public sealed class LinearFeedProjectTests
{
    private static readonly DateTimeOffset _at = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly SourceId _source = SourceId.From("linear");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static JsonObject InProject(string id, string projectStatus = "started") =>
        Issue(id, _at, n => n["project"] = new JsonObject { ["id"] = "pr1", ["name"] = "Launch", ["status"] = new JsonObject { ["type"] = projectStatus } });

    private static JsonObject Issue(string id, DateTimeOffset updatedAt, Action<JsonObject>? shape = null)
    {
        var node = FakeLinearIssues.Node(id, updatedAt);
        shape?.Invoke(node);
        return node;
    }

    private static async Task<RecordItem> One(JsonObject node)
    {
        var (feed, _) = LinearFeedContractTests.FeedOf([node]);
        var page = await feed.Pull(new RecordPull(After: null, 10, NotBefore: null), Ct);
        return Assert.Single(page.Value.Items);
    }

    [Fact]
    public async Task An_issue_in_a_project_links_to_it_with_the_project_name_and_one_without_has_no_such_link()
    {
        var inProject = await One(InProject("a1"));
        var alone = await One(Issue("a2", _at));

        Assert.Equal([("linear:project/pr1", RecordLinkKind.InProject, "Launch")], inProject.Links.Select(l => (l.ToRef, l.Kind, l.Title)));
        Assert.DoesNotContain(alone.Links, static l => l.Kind == RecordLinkKind.InProject);
    }

    [Fact]
    public async Task The_version_is_the_updatedAt_followed_by_the_feed_format_suffix()
    {
        var item = await One(Issue("a1", _at));

        Assert.Equal("2026-10-01T12:00:00.0000000Z;f2", item.Version.Value);
    }

    [Fact]
    public async Task A_stored_issue_with_the_old_version_is_saved_again_once_and_then_skipped()
    {
        var store = new FakeRecordStore();
        var old = RecordSamples.Record("linear:issue/a1", version: "2026-10-01T12:00:00.0000000Z", source: _source);
        _ = await store.Commit(RecordSamples.Batch(_source, 0, RecordSamples.Write(old, "Text a1")), Ct);
        RecordSyncRun Run() => new(LinearFeedContractTests.FeedOf([InProject("a1")]).Feed, store, (new FakeScrubberFactory().For([]).Value, new ContextIndexSettings()), new FakeTimeProvider(_at.AddDays(1)), RecordSyncRun.PageSize);

        var first = await Run().Execute(Ct);
        _ = await store.Commit(RecordSamples.Batch(_source, 0), Ct);   // the position goes back to the start, as a reset cursor would leave it
        var second = await Run().Execute(Ct);

        Assert.Equal((1, 0), (first.Records, first.Unchanged));
        Assert.Equal((0, 1), (second.Records, second.Unchanged));
    }

    [Fact]
    public async Task The_first_Recent_read_with_no_watermark_asks_for_the_window_or_open_issues_of_open_projects_inside_the_teams()
    {
        var (feed, fake) = LinearFeedContractTests.FeedOf([Issue("a1", _at)]);
        var start = _at.AddDays(-30);

        _ = await feed.Pull(new RecordPull(After: null, 10, start), Ct);

        var filter = fake.Variables[0]["filter"]!.AsObject();
        Assert.Equal("SPL", filter["team"]?["key"]?["in"]?[0]?.GetValue<string>());
        Assert.Null(filter["updatedAt"]);
        Assert.Equal(
            """[{"updatedAt":{"gte":"2026-09-01T12:00:00.0000000Z"}},{"state":{"type":{"nin":["completed","canceled"]}},"project":{"status":{"type":{"nin":["completed","canceled"]}}}}]""",
            filter["or"]!.ToJsonString());
    }

    [Fact]
    public async Task An_old_open_issue_of_an_open_project_comes_in_on_the_first_read_and_a_closed_project_s_does_not()
    {
        var old = _at.AddDays(-200);
        var (feed, _) = LinearFeedContractTests.FeedOf([Issue("recent", _at), Aged(InProject("keep"), old), Aged(InProject("gone", "completed"), old), Issue("loose", old)]);

        var page = await feed.Pull(new RecordPull(After: null, 10, _at.AddDays(-30)), Ct);

        Assert.Equal(["linear:issue/recent", "linear:issue/keep"], page.Value.Items.Select(i => i.Key.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_later_Recent_read_a_Backfill_read_and_an_Incremental_read_never_carry_the_clause()
    {
        var (feed, fake) = LinearFeedContractTests.FeedOf([Issue("a1", _at)]);
        var start = _at.AddDays(-30);
        var watermark = SourceCursor.From("s=2026-10-01T12:00:00.0000000Z;a=;t=");

        _ = await feed.Pull(new RecordPull(watermark, 10, start), Ct);                  // Recent or Incremental with a watermark
        _ = await feed.Pull(new RecordPull(After: null, 10, start.AddDays(-150), start), Ct);  // Backfill: no watermark, bounded above
        _ = await feed.Pull(new RecordPull(watermark, 10, start.AddDays(-150)), Ct);    // Incremental

        Assert.All(fake.Variables, static v => Assert.Null(v["filter"]?["or"]));
        Assert.All(fake.Variables, static v => Assert.NotNull(v["filter"]?["updatedAt"]));
    }

    private static JsonObject Aged(JsonObject node, DateTimeOffset at)
    {
        node["updatedAt"] = at.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        return node;
    }
}
