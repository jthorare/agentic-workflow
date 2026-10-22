using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Agents;
using Aw.Sindri.Ui.ViewModels.Controls;
using static Aw.Sindri.Ui.Tests.Agents.Page.AgentsPageFixture;

namespace Aw.Sindri.Ui.Tests.Agents.Page;

/// <summary>The Agents page's list (collector spec CU-1, CU-6, U11a T1): the served counts, groups, rows, chips, problems and states.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentsPageViewModelTests
{
    private const string Claude = "claude-code/";
    private const string Codex = "codex/";
    private const string Cursor = "cursor/";
    private readonly AgentsPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SessionRowViewModel RowOf(AgentsPageViewModel page, string prefix) =>
        page.Groups.SelectMany(g => g.Rows).Single(r => r.Ref.StartsWith(prefix, StringComparison.Ordinal));

    // The provider part of each ref, with its slash: the same text the constants hold.
    private static IEnumerable<string> Providers(IEnumerable<SessionRowViewModel> rows) => rows.Select(r => r.Ref[..(r.Ref.IndexOf('/', StringComparison.Ordinal) + 1)]);

    [Fact]
    public async Task Head_counts_are_the_served_counts_and_ignore_filters()
    {
        var counts = Golden("sessions", "fixture-three-providers")["counts"]!;
        var page = await _f.Shown(Token);
        var expected = Labels.AgentsHead((int)counts["live"]!, (int)counts["asking"]!);

        Assert.Equal(expected, page.Head);
        page.FilterProvider("Codex");
        page.FilterState("idle");
        Assert.Equal(expected, page.Head);
    }

    [Fact]
    public async Task Rows_without_a_launch_sit_in_outside_a_workflow()
    {
        var page = await _f.Shown(Token);

        var outside = page.Groups[1];
        Assert.Equal(Labels.OutsideWorkflow, outside.Name);
        Assert.Equal([Claude, Codex], Providers(outside.Rows), StringComparer.Ordinal);
        Assert.Equal(Labels.GroupHeader(Labels.OutsideWorkflow, 2), outside.Header);
    }

    [Fact]
    public async Task A_launched_row_sits_in_the_started_by_sindri_group()
    {
        var page = await _f.Shown(Token);

        var launched = page.Groups[0];
        Assert.Equal(Labels.StartedBySindri, launched.Name);
        var row = Assert.Single(launched.Rows);
        Assert.Equal(Labels.LaunchLine, row.LaneText);
    }

    [Fact]
    public async Task Asking_row_reads_asking_reason_and_tool()
    {
        var served = Row(Golden("sessions", "fixture-three-providers"), Codex)["asking"]!;
        var page = await _f.Shown(Token);

        var row = RowOf(page, Codex);

        Assert.Equal("◐ Asking you", row.ChipText);
        Assert.Equal(Labels.AskingDetail((string)served["reason"]!, (string)served["tool"]!), row.AskingText);
        Assert.Contains(row.AskingText, row.Name, StringComparison.Ordinal);
        Assert.Contains("asking", row.Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Transcript_source_reads_estimated()
    {
        _f.Serve(Edited(l =>
        {
            Row(l, Claude)["stateSource"] = "transcript";
            Row(l, Claude)["estimated"] = true;
            Row(l, Codex)["stateSource"] = "transcript";
            Row(l, Codex)["estimated"] = false;
        }));
        var page = await _f.Shown(Token);

        Assert.Equal("estimated", RowOf(page, Claude).EstimatedText);
        Assert.Equal(string.Empty, RowOf(page, Codex).EstimatedText);
        Assert.Contains("estimated", RowOf(page, Claude).Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_meta_lane_reads_meta_team_slash_lane()
    {
        var page = await _f.Shown(Token);

        Assert.Equal("Lane sindri/ideas", RowOf(page, Claude).LaneText);
        Assert.Equal(string.Empty, RowOf(page, Codex).LaneText);
    }

    [Fact]
    public async Task A_row_with_no_lane_and_a_tmux_session_shows_the_session_after_the_host()
    {
        _f.Serve(Edited(l => Row(l, Claude)["lane"] = null));
        var page = await _f.Shown(Token);

        Assert.Equal("tmux · Warp · main", RowOf(page, Claude).HostText);
        Assert.Equal("Warp", RowOf(page, Codex).HostText);
    }

    [Fact]
    public async Task No_title_reads_repo_slash_id8()
    {
        var page = await _f.Shown(Token);

        Assert.Equal("private title", RowOf(page, Claude).TitleText);
        Assert.Equal("web/bbbbbbbb", RowOf(page, Codex).TitleText);
    }

    [Fact]
    public async Task No_repo_and_no_title_reads_id8()
    {
        var page = await _f.Shown(Token);

        Assert.Equal("pid-333-", RowOf(page, Cursor).TitleText);
    }

    [Fact]
    public async Task Problems_render_code_and_text_above_the_list()
    {
        _f.Serve("problems-source-failed");
        var served = Golden("sessions", "problems-source-failed")["problems"]![0]!;
        var page = await _f.Shown(Token);

        Assert.Equal([(string)served["code"]! + " " + (string)served["text"]!], page.ProblemLines);
        Assert.Equal(3, page.Groups.Sum(g => g.Rows.Count));
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("proc-unreadable")]
    [InlineData("unsupported-platform")]
    public async Task Session_002_replaces_the_list_with_the_page_line(string scenario)
    {
        var page = await _f.Shown(Token);
        _f.Server.SessionAnswer = RouteAnswer.Problem("sessions", scenario);

        await page.Refresh(Url, Token);

        Assert.Equal(Labels.SessionsUnavailable((string)Golden("sessions", scenario)["detail"]!), page.UnavailableText);
        Assert.True(page.IsUnavailable);
        Assert.Empty(page.Groups);
        Assert.Empty(page.Ended);
        Assert.False(page.HasHead);
        Assert.False(page.FiltersEnabled);
    }

    [Fact]
    public async Task The_list_comes_back_after_a_002()
    {
        _f.Server.SessionAnswer = RouteAnswer.Problem("sessions", "disabled");
        var page = await _f.Shown(Token);
        _f.Server.SessionAnswer = null;

        await page.Refresh(Url, Token);

        Assert.False(page.IsUnavailable);
        Assert.Equal(2, page.Groups.Count);
        Assert.True(page.FiltersEnabled);
    }

    [Fact]
    public async Task Empty_list_shows_the_served_sentence()
    {
        _f.Serve("empty");
        var page = await _f.Shown(Token);

        Assert.True(page.IsEmpty);
        Assert.Equal((string)Golden("sessions", "empty")["empty"]!, page.EmptyText);
        Assert.Empty(page.Groups);
        Assert.Equal(Labels.CursorChatsNotShown, page.Footer);
    }

    [Fact]
    public async Task A_filter_with_no_match_shows_the_apps_sentence()
    {
        var page = await _f.Shown(Token);

        page.FilterProvider("Codex");
        page.FilterState("idle");

        Assert.True(page.IsEmpty);
        Assert.Equal(Labels.NoSessionsMatch, page.EmptyText);
        Assert.Empty(page.Groups);
    }

    [Fact]
    public async Task Filters_narrow_by_provider_state_and_repo_over_one_read()
    {
        var page = await _f.Shown(Token);
        var reads = _f.Server.SessionsCalls;

        page.FilterProvider("Codex");
        Assert.Equal([Codex], Providers(page.Groups.SelectMany(g => g.Rows)), StringComparer.Ordinal);
        page.FilterProvider(Labels.AllProviders);
        page.FilterState("running");
        Assert.Equal([Claude], Providers(page.Groups.SelectMany(g => g.Rows)), StringComparer.Ordinal);
        page.FilterState(choice: null);
        page.FilterRepo("web");
        Assert.Equal([Codex], Providers(page.Groups.SelectMany(g => g.Rows)), StringComparer.Ordinal);
        Assert.Equal(reads, _f.Server.SessionsCalls);
        Assert.Contains("api", page.RepoChoices, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Recently_ended_is_folded_and_copies_its_resume()
    {
        _f.Serve("recently-ended");
        var served = Golden("sessions", "recently-ended")["recentlyEnded"]![0]!["resume"]!.AsArray().Select(a => (string)a!);
        var page = await _f.Shown(Token);

        var ended = Assert.Single(page.Ended);
        Assert.False(page.IsEndedExpanded);
        Assert.Equal(Labels.RecentlyEndedHeader(1), page.EndedHeader);
        Assert.Equal(string.Join(' ', served), ended.ResumeText);
        await ended.Copy(Token);
        Assert.Equal(string.Join(' ', served), _f.Clipboard.Text);
    }

    [Fact]
    public async Task A_resume_argument_with_whitespace_or_a_quote_is_single_quoted()
    {
        _f.Serve(Edited(l =>
        {
            var ended = Golden("sessions", "recently-ended")["recentlyEnded"]![0]!.DeepClone();
            ended["resume"] = new JsonArray("claude", "--name", "my lane", "it's");
            l["recentlyEnded"] = new JsonArray(ended);
        }));
        var page = await _f.Shown(Token);

        Assert.Equal("claude --name 'my lane' 'it'\\''s'", Assert.Single(page.Ended).ResumeCommand);
    }

    [Fact]
    public async Task Null_resume_reads_no_resume_command()
    {
        _f.Serve(Edited(l =>
        {
            var ended = Golden("sessions", "recently-ended")["recentlyEnded"]![0]!.DeepClone();
            ended["resume"] = null;
            l["recentlyEnded"] = new JsonArray(ended);
        }));
        var page = await _f.Shown(Token);

        var row = Assert.Single(page.Ended);
        Assert.Equal(Labels.NoResumeCommand, row.ResumeText);
        Assert.False(row.CanCopy);
        await row.Copy(Token);
        Assert.Null(_f.Clipboard.Text);
    }

    [Fact]
    public async Task Refresh_keeps_the_selected_row_by_ref()
    {
        var page = await _f.Shown(Token);
        var codex = RowOf(page, Codex);
        Assert.True(page.Select(codex.Ref));
        _f.Serve(Edited(l => Row(l, Claude)["title"] = "renamed"));

        await page.Refresh(Url, Token);

        Assert.Same(codex, RowOf(page, Codex));
        Assert.Equal(codex.Ref, page.SelectedKey);
        Assert.True(codex.IsSelected);
        Assert.Equal("renamed", RowOf(page, Claude).TitleText);
    }

    [Fact]
    public async Task A_row_that_leaves_moves_selection_to_its_neighbour()
    {
        var page = await _f.Shown(Token);
        var neighbour = RowOf(page, Claude).Ref;
        _ = page.Select(RowOf(page, Codex).Ref);

        _f.Serve(Edited(l => Remove(l, Codex)));
        await page.Refresh(Url, Token);
        Assert.Equal(neighbour, page.SelectedKey);

        _f.Serve(Edited(l => { Remove(l, Codex); Remove(l, Cursor); }));
        await page.Refresh(Url, Token);
        Assert.Equal(RowOf(page, Claude).Ref, page.SelectedKey);
    }

    [Fact]
    public async Task Live_text_changes_only_when_counts_change()
    {
        var page = await _f.Shown(Token);
        var said = new List<string>();
        page.Voice.PropertyChanged += (_, e) => said.Add(e.PropertyName!);

        await page.Refresh(Url, Token);
        await page.Refresh(Url, Token);
        Assert.Empty(said);
        Assert.False(page.Voice.IsAnnouncing);

        _f.Serve(Edited(l => l["counts"] = new JsonObject { ["live"] = 4, ["asking"] = 2, ["outsideWorkflow"] = 3 }));
        await page.Refresh(Url, Token);
        Assert.True(page.Voice.IsAnnouncing);
        Assert.Equal(Labels.AgentsCounts(4, 2), page.Voice.Announcement);

        said.Clear();
        await page.Refresh(Url, Token);
        Assert.Empty(said);
    }

    [Fact]
    public async Task Focus_is_the_catalogs_pending_control_with_the_rows_ref_in_its_cli()
    {
        var page = await _f.Shown(Token);

        var focus = RowOf(page, Claude).Focus;

        Assert.Equal("Coming soon · sindri agents focus " + RowOf(page, Claude).Ref, focus.Line);
        Assert.Equal("sindri agents focus " + RowOf(page, Claude).Ref, focus.Cli);
        Assert.Equal("Watch the agent, coming soon", focus.Name);
    }

    [Fact]
    public async Task A_ready_focus_with_no_sender_draws_as_pending_until_the_send_lands()
    {
        var ready = new AgentsPageFixture(focusUi: "ready");

        var page = await ready.Shown(Token);

        Assert.Equal("Watch the agent, coming soon", RowOf(page, Claude).Focus.Name);
    }

    [Fact]
    public async Task Without_a_catalog_row_focus_still_says_its_cli()
    {
        var page = new AgentsPageViewModel(_f.Server, _f.Clipboard, _f.Clock, new CatalogViewModel(_f.Clipboard, _f.Clock));
        page.SetShown(shown: true);
        await page.Refresh(Url, Token);

        Assert.Equal("sindri agents focus " + RowOf(page, Claude).Ref, RowOf(page, Claude).Focus.Cli);
    }

    [Fact]
    public async Task A_read_that_cannot_be_read_keeps_the_rows_and_marks_them_stale()
    {
        var page = await _f.Shown(Token);
        _f.Server.SessionAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, "{\"sessions\": 3}");

        await page.Refresh(Url, Token);

        Assert.True(page.IsUnreadable);
        Assert.True(page.IsStale);
        Assert.Equal(2, page.Groups.Count);
    }

    [Fact]
    public async Task A_dropped_connection_keeps_the_rows_and_marks_them_stale()
    {
        var page = await _f.Shown(Token);
        _f.Server.GoDown();

        await page.Refresh(Url, Token);

        Assert.True(page.IsStale);
        Assert.False(page.IsUnreadable);
        Assert.Equal(2, page.Groups.Count);
    }

    [Fact]
    public async Task The_column_captions_show_only_when_there_are_rows()
    {
        var page = await _f.Shown(Token);

        Assert.True(page.HasColumns);
        Assert.Equal(("Status", "Agent", "Where it runs", "What it is doing", "For"), (page.Columns.Status, page.Columns.Agent, page.Columns.Where, page.Columns.Doing, page.Columns.For));

        _f.Serve("empty");
        var empty = await _f.Shown(Token);

        Assert.False(empty.HasColumns);
    }
}
