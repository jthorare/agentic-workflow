using System.Text.Json.Nodes;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;

namespace Aw.Context.Tests.Linear;

/// <summary>What the Linear feed makes of one issue: its state (Linear's word kept), dates, audience and links.</summary>
public sealed class LinearFeedMappingTests
{
    private static readonly DateTimeOffset _at = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static async Task<RecordItem> One(Action<JsonObject> shape)
    {
        var node = FakeLinearIssues.Node("a1", _at);
        shape(node);
        var (feed, _) = LinearFeedContractTests.FeedOf([node]);
        var page = await feed.Pull(new RecordPull(After: null, 10, NotBefore: null), TestContext.Current.CancellationToken);
        return Assert.Single(page.Value.Items);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("triage", "Triage", RecordState.Open)]
    [InlineData("backlog", "Backlog", RecordState.Open)]
    [InlineData("unstarted", "Todo", RecordState.Open)]
    [InlineData("started", "In Review", RecordState.Open)]
    [InlineData("completed", "Done", RecordState.Done)]
    [InlineData("canceled", "Won't fix", RecordState.Canceled)]
    public async Task The_state_type_maps_to_a_record_state_and_the_raw_name_is_kept(string type, string name, RecordState expected)
    {
        var item = await One(n => n["state"] = new JsonObject { ["name"] = name, ["type"] = type });

        Assert.Equal((expected, name), (item.State, item.StateRaw));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_completed_issue_closes_at_its_completedAt_and_the_item_carries_key_title_text_author_team_audience_and_version()
    {
        var item = await One(n =>
        {
            n["state"] = new JsonObject { ["name"] = "Done", ["type"] = "completed" };
            n["completedAt"] = "2026-10-02T08:00:00Z";
            n["creator"] = new JsonObject { ["id"] = "u1", ["name"] = "Ada" };
        });

        Assert.Equal(("linear:issue/a1", RecordKind.Issue, "Title a1", "Text a1", "Ada"), (item.Key.Value, item.Kind, item.Title, item.Text, item.Author));
        Assert.Equal(new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.Zero), item.ClosedAt);
        Assert.Equal("linear:SPL", item.Audience?.Value);
        Assert.Equal(_at, item.UpdatedAt);
        Assert.False(string.IsNullOrEmpty(item.Version.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_parent_and_the_relations_become_links_and_a_relation_from_the_other_side_adds_only_a_related_link()
    {
        var item = await One(n =>
        {
            n["parent"] = new JsonObject { ["id"] = "p1", ["identifier"] = "SPL-p1", ["title"] = "Epic" };
            n["relations"] = new JsonObject
            {
                ["nodes"] = new JsonArray(
                    new JsonObject { ["type"] = "blocks", ["relatedIssue"] = new JsonObject { ["id"] = "b1", ["identifier"] = "SPL-b1", ["title"] = "Blocked" } },
                    new JsonObject { ["type"] = "duplicate", ["relatedIssue"] = new JsonObject { ["id"] = "d1", ["identifier"] = "SPL-d1", ["title"] = "Dup" } },
                    new JsonObject { ["type"] = "related", ["relatedIssue"] = new JsonObject { ["id"] = "r1", ["identifier"] = "SPL-r1", ["title"] = "Rel" } }),
            };
            n["inverseRelations"] = new JsonObject
            {
                ["nodes"] = new JsonArray(
                    new JsonObject { ["type"] = "blocks", ["issue"] = new JsonObject { ["id"] = "x1", ["identifier"] = "SPL-x1", ["title"] = "Blocker" } },
                    new JsonObject { ["type"] = "related", ["issue"] = new JsonObject { ["id"] = "r2", ["identifier"] = "SPL-r2", ["title"] = "Rel2" } }),
            };
        });

        Assert.Equal(
            [
                ("linear:issue/p1", RecordLinkKind.Parent, "Epic"),
                ("linear:issue/b1", RecordLinkKind.Blocks, "Blocked"),
                ("linear:issue/d1", RecordLinkKind.Duplicates, "Dup"),
                ("linear:issue/r1", RecordLinkKind.Relates, "Rel"),
                ("linear:issue/r2", RecordLinkKind.Relates, "Rel2"),
            ],
            item.Links.Select(l => (l.ToRef, l.Kind, l.Title)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_finished_run_commits_the_newest_updatedAt_and_the_next_pull_asks_only_for_what_changed_after_it()
    {
        var older = FakeLinearIssues.Node("o", _at.AddDays(-1));
        var newest = FakeLinearIssues.Node("n", _at);
        var (feed, fake) = LinearFeedContractTests.FeedOf([older, newest]);
        var ct = TestContext.Current.CancellationToken;

        var first = (await feed.Pull(new RecordPull(After: null, 10, NotBefore: null), ct)).Value;
        var again = (await feed.Pull(new RecordPull(first.Next, 10, NotBefore: null), ct)).Value;

        Assert.Equal(["n", "o"], first.Items.Select(i => i.Key.Value["linear:issue/".Length..]), StringComparer.Ordinal);
        Assert.Empty(again.Items);
        Assert.NotNull(fake.Variables[^1]["filter"]?["updatedAt"]?["gt"]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_window_start_is_sent_as_a_filter_on_updatedAt_and_the_teams_filter_is_the_profiles()
    {
        var (feed, fake) = LinearFeedContractTests.FeedOf([FakeLinearIssues.Node("n", _at)]);

        _ = await feed.Pull(new RecordPull(After: null, 10, _at.AddDays(-1)), TestContext.Current.CancellationToken);

        Assert.NotNull(fake.Variables[0]["filter"]?["or"]?[0]?["updatedAt"]?["gte"]);
        Assert.Equal("SPL", fake.Variables[0]["filter"]?["team"]?["key"]?["in"]?[0]?.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_text_is_the_description_and_the_comments_the_way_the_linear_source_joins_them()
    {
        var item = await One(n => n["comments"] = new JsonObject
        {
            ["nodes"] = new JsonArray(
                new JsonObject { ["id"] = "c1", ["body"] = "Decided: ship the flag off.", ["createdAt"] = "2026-10-01T09:00:00Z", ["user"] = new JsonObject { ["id"] = "u1", ["name"] = "Ada" } }),
            ["pageInfo"] = new JsonObject { ["hasNextPage"] = false },
        });

        Assert.StartsWith("Text a1", item.Text, StringComparison.Ordinal);
        Assert.Contains("- Ada (2026-10-01): Decided: ship the flag off.", item.Text, StringComparison.Ordinal);
    }
}
