using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>
/// The since-you-looked card (#2536 Part 2): fixed groups, one row per issue with every change on it, newest first, the words and chips a row carries,
/// the empty and error states, and one count everywhere. The clock stands at 2026-01-01T00:00Z; the "three" golden's changes are at 22:10, 23:30 and 23:40 the day before.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class LinearSinceCardTests
{
    private readonly LinearFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonNode Since(params (string Kind, string Key, string Title, string At, string? From, string? To)[] items)
    {
        var rows = new JsonArray();
        foreach (var (kind, key, title, at, from, to) in items)
        {
            rows.Add(new JsonObject { ["kind"] = kind, ["key"] = key, ["title"] = title, ["actor"] = null, ["at"] = at, ["from"] = from, ["to"] = to });
        }

        return new JsonObject { ["marker"] = "lin_08de48c5e9b68800", ["count"] = items.Length, ["items"] = rows };
    }

    private async Task<LinearWorkViewModel> ShownWith(JsonNode since)
    {
        _f.Server.LinearSinceAnswer = LinearFixture.Served(since);
        return await _f.Shown(Token);
    }

    [Fact]
    public async Task The_groups_are_Assigned_to_you_then_Moved_and_a_group_with_no_issue_is_left_out()
    {
        var work = await _f.Shown(Token);

        Assert.Equal(["Assigned to you", "Moved"], work.StripGroups.Select(g => g.Heading), StringComparer.Ordinal);
        Assert.Equal([["ENG-31"], ["ENG-12"]], work.StripGroups.Select(g => g.Rows.Select(r => r.Key).ToArray()));
    }

    [Fact]
    public async Task A_mention_alone_sits_in_Other()
    {
        var work = await ShownWith(Since(("mentioned", "OPS-40", "Rotate the staging deploy key", "2025-12-31T23:00:00Z", null, null)));

        Assert.Equal(["Other"], work.StripGroups.Select(g => g.Heading), StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_issue_assigned_and_moved_is_one_row_in_Assigned_that_lists_both_changes_newest_first()
    {
        var work = await ShownWith(Since(
            ("assigned", "FRN-6177", "Retry budget", "2025-12-31T22:00:00Z", null, null),
            ("state", "FRN-6177", "Retry budget", "2025-12-31T23:00:00Z", "Todo", "In Progress")));

        var row = Assert.Single(work.StripRows);
        Assert.Equal((LinearSinceGroup.Assigned, "FRN-6177 · moved from Todo to In Progress · assigned to you", 1), (row.Group, row.Detail, work.NewCount));
    }

    [Fact]
    public async Task Rows_run_newest_change_first_inside_each_group()
    {
        var work = await ShownWith(Since(
            ("mentioned", "A-1", "Old mention", "2025-12-31T10:00:00Z", null, null),
            ("mentioned", "A-3", "Newest mention", "2025-12-31T23:00:00Z", null, null),
            ("mentioned", "A-2", "Middle mention", "2025-12-31T18:00:00Z", null, null)));

        Assert.Equal(["A-3", "A-2", "A-1"], work.StripGroups.Single().Rows.Select(r => r.Key), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_row_says_the_title_the_time_and_the_chips_the_list_knows()
    {
        var work = await _f.Shown(Token);

        var moved = work.StripRows.Single(r => string.Equals(r.Key, "ENG-12", StringComparison.Ordinal));
        Assert.Equal(
            ("Retry budget for the sync worker", "20m ago", "Todo → In Progress", "Urgent", "Sync worker", "JE"),
            (moved.Title, moved.TimeText, moved.StateChip, moved.PriorityChip, moved.Project, moved.Initials));
        Assert.Equal("ENG-12, Retry budget for the sync worker, moved from Todo to In Progress, Sam Lee mentioned you, 20m ago", moved.Name);
    }

    [Fact]
    public async Task An_issue_the_list_does_not_hold_has_no_priority_project_or_initials_chip_and_no_blank_one()
    {
        var work = await ShownWith(Since(("state", "ZZZ-1", "Not in the list", "2025-12-31T23:00:00Z", "Todo", "Done")));

        var row = Assert.Single(work.StripRows);
        Assert.Equal((false, false, false, true), (row.HasPriorityChip, row.HasProject, row.HasInitials, row.HasStateChip));
    }

    [Fact]
    public async Task A_title_of_sixty_characters_or_more_is_kept_whole_for_the_view_to_trim()
    {
        var title = new string('x', 80);

        var work = await ShownWith(Since(("assigned", "ENG-31", title, "2025-12-31T23:00:00Z", null, null)));

        Assert.Equal(title, work.StripRows.Single().Title);
    }

    [Fact]
    public async Task The_header_the_tab_and_the_rows_say_the_same_number()
    {
        var work = await _f.Shown(Token);

        Assert.Equal((2, 2, "Since you last looked, 2 items", "Linear · 2 new"), (work.NewCount, work.StripRows.Count, work.StripName, work.LinearTabText));
    }

    [Fact]
    public async Task Nothing_new_says_so_and_after_Mark_all_seen_says_when_the_newest_change_you_saw_was()
    {
        var work = await _f.Shown(Token);
        await work.MarkAllSeen();
        _f.Server.LinearSinceAnswer = RouteAnswer.Api("linear-since", "empty");
        await work.Refresh(LinearFixture.Url, Token);

        Assert.Equal((true, false, "Nothing new since you last looked"), (work.ShowsNothingNew, work.HasNew, work.NothingNewText));
        Assert.EndsWith(" ago", work.NothingNewWhen, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nothing_new_with_no_marker_yet_has_no_time_to_say()
    {
        _f.Server.LinearSinceAnswer = RouteAnswer.Api("linear-since", "empty");

        var work = await _f.Shown(Token);

        Assert.Equal((true, string.Empty), (work.ShowsNothingNew, work.NothingNewWhen));
    }

    private static RouteAnswer FeedFailing(string code)
    {
        var mine = LinearFixture.Golden("mine");
        mine["feed"] = new JsonObject { ["state"] = "failing", ["lastSyncedAt"] = "2025-12-31T22:05:00.0000000Z", ["code"] = code, ["reread"] = null };
        return LinearFixture.Served(mine);
    }

    [Fact]
    public async Task A_feed_that_failed_with_a_refused_key_shows_the_Reconnect_card_with_the_reason_in_words()
    {
        _f.Mine = FeedFailing("SND-CONTEXT-071");

        var work = await _f.Shown(Token);

        Assert.Equal((true, true, false, false, "Linear refused the key. It may have expired or been revoked."), (work.ShowsStrip, work.StripNeedsReconnect, work.StripFeedFailing, work.ShowsNothingNew, work.StripReason));
    }

    [Theory]
    [InlineData("SND-CONTEXT-010", "Linear is rate-limiting Sindri or is down. Sindri keeps trying.")]
    [InlineData("SND-CONTEXT-072", "Linear sent a reply Sindri couldn't read.")]
    public async Task Any_other_feed_failure_shows_the_reason_and_Retry_and_no_Reconnect(string code, string reason)
    {
        _f.Mine = FeedFailing(code);

        var work = await _f.Shown(Token);

        Assert.Equal((true, false, true, reason), (work.ShowsStrip, work.StripNeedsReconnect, work.StripFeedFailing, work.StripReason));
    }

    [Fact]
    public async Task A_failed_read_of_the_changes_is_its_own_small_state_with_Retry_and_no_Reconnect()
    {
        _f.Server.LinearSinceAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-LINEAR-001", "The store can't be read.");

        var work = await _f.Shown(Token);

        Assert.Equal((true, true, false, false, false), (work.ShowsStrip, work.StripFailed, work.StripNeedsReconnect, work.StripFeedFailing, work.ShowsNothingNew));
    }

    [Fact]
    public async Task Reconnect_asks_for_the_Connections_page()
    {
        var work = await _f.Shown(Token);
        var asked = 0;
        work.ConnectRequested += (_, _) => asked++;

        work.Reconnect();

        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task Not_connected_shows_no_card_and_disables_New_issue_with_the_reason()
    {
        _f.Mine = RouteAnswer.Api("linear-issues", "not-connected");
        _f.Teams = RouteAnswer.Api("linear-issues", "not-connected");

        var work = await _f.Shown(Token);

        Assert.Equal((false, false, "Connect Linear first: New issue needs a Linear connection."), (work.ShowsStrip, work.CanCreate, work.CreateTip));
    }

    [Fact]
    public async Task New_issue_is_enabled_while_Linear_is_connected()
    {
        var work = await _f.Shown(Token);

        Assert.Equal((true, string.Empty), (work.CanCreate, work.CreateTip));
    }

    [Fact]
    public async Task The_card_is_hidden_on_an_older_Sindri_that_has_no_changes_feed()
    {
        _f.Server.LinearSinceAnswer = new RouteAnswer(System.Net.HttpStatusCode.NotFound, string.Empty);

        var work = await _f.Shown(Token);

        Assert.Equal((false, false), (work.ShowsStrip, work.ShowsNothingNew));
    }

    [Fact]
    public async Task Rows_pick_up_the_list_when_it_arrives_after_the_changes()
    {
        var work = _f.Work();
        work.Choose(linear: true);
        await work.Refresh(LinearFixture.Url, Token);   // Work not shown: the changes are read, the list is not
        var row = work.StripRows.Single(r => string.Equals(r.Key, "ENG-31", StringComparison.Ordinal));
        Assert.Equal(string.Empty, row.PriorityChip);

        work.SetShown(shown: true);
        await work.CatchUp;

        Assert.Equal("High", row.PriorityChip);
    }
}
