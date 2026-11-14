using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Page;

/// <summary>The Agents list (#3082, #3153, #3024): a pinned Needs you group, an order that holds, a row named by what it is, a search box, and keys to jump between asking rows.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentsPageNeedsYouTests
{
    private const string Claude = "claude-code/";
    private const string Codex = "codex/";

    private readonly AgentsPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonObject Add(JsonNode list, string from, string suffix, string? title = null, string? repo = null, string? since = null, string? lane = null)
    {
        var copy = AgentsPageFixture.Row(list, from).DeepClone().AsObject();
        copy["ref"] = from + "dddddddd-0000-4000-8000-" + suffix;
        copy["id"] = "dddddddd-0000-4000-8000-" + suffix;
        copy["title"] = title;
        if (repo is not null)
        {
            copy["repo"] = repo;
        }

        if (since is not null)
        {
            copy["since"] = since;
        }

        if (lane is not null)
        {
            copy["lane"] = new JsonObject { ["team"] = "sindri", ["lane"] = lane };
        }

        list["sessions"]!.AsArray().Add(copy);
        return copy;
    }

    private static List<string> Titles(SessionGroupViewModel group) => [.. group.Rows.Select(r => r.TitleText)];

    private static SessionGroupViewModel Outside(AgentsPageViewModel page) => page.Groups.Single(g => g.Name == "Not part of a workflow");

    [Fact]
    public async Task The_asking_rows_are_a_pinned_Needs_you_group_on_top_and_no_other_group_repeats_them_3082()
    {
        var page = await _f.Shown(Token);

        var first = page.Groups[0];

        Assert.Equal(("Needs you", true, "Needs you (1)"), (first.Name, first.Rows.All(r => r.IsAsking), first.Header));
        Assert.DoesNotContain(page.Groups.Skip(1).SelectMany(g => g.Rows), r => r.IsAsking);
        Assert.Equal(3, page.Groups.Sum(g => g.Rows.Count));
    }

    [Fact]
    public async Task With_nobody_asking_there_is_no_Needs_you_group_3082()
    {
        _f.Serve(AgentsPageFixture.Edited(l => AgentsPageFixture.Remove(l, Codex)));

        var page = await _f.Shown(Token);

        Assert.DoesNotContain(page.Groups, g => g.Name == "Needs you");
    }

    [Fact]
    public async Task The_default_order_is_by_name_and_does_not_move_when_a_row_ages_3153()
    {
        var list = AgentsPageFixture.Edited(_ => { });
        _ = Add(list, Claude, "000000000011", title: "zeta");
        _ = Add(list, Claude, "000000000012", title: "alpha");
        _f.Serve(list);
        var page = await _f.Shown(Token);
        var before = Titles(Outside(page));

        Add(list, Claude, "000000000013", title: "mid", since: "2026-10-16T11:00:00.0000000Z");
        _f.Serve(list);
        await page.Refresh(AgentsPageFixture.Url, Token);

        Assert.Equal(["alpha", "private title", "zeta"], before);
        Assert.Equal(["alpha", "mid", "private title", "zeta"], Titles(Outside(page)));
    }

    [Fact]
    public async Task A_picked_order_holds_across_reads_3153()
    {
        var list = AgentsPageFixture.Edited(_ => { });
        _ = Add(list, Claude, "000000000011", title: "zeta", repo: "api", since: "2026-10-16T10:00:00.0000000Z");
        _ = Add(list, Claude, "000000000012", title: "alpha", repo: "web", since: "2026-10-16T11:00:00.0000000Z");
        _f.Serve(list);
        var page = await _f.Shown(Token);

        page.SetSort(AgentSort.Age);
        var byAge = Titles(Outside(page));
        await page.Refresh(AgentsPageFixture.Url, Token);
        var afterRead = Titles(Outside(page));
        page.SetSort(AgentSort.Repo);

        Assert.Equal(("zeta", "zeta"), (byAge[0], afterRead[0]));
        Assert.Equal(["private title", "zeta", "alpha"], Titles(Outside(page)));   // repo api (two rows, by name), then web
        Assert.Equal(AgentSort.Repo, page.Sort);
    }

    [Fact]
    public async Task An_estimated_state_is_held_for_a_few_seconds_before_the_row_changes_it_3082()
    {
        var list = AgentsPageFixture.Edited(l => Set(l, Claude, "running", estimated: true));
        _f.Serve(list);
        var page = await _f.Shown(Token);
        Set(list, Claude, "idle", estimated: true);
        _f.Serve(list);

        await page.Refresh(AgentsPageFixture.Url, Token);
        var soon = Row(page, Claude).StateWord;
        _f.Clock.Advance(TimeSpan.FromSeconds(6));
        await page.Refresh(AgentsPageFixture.Url, Token);

        Assert.Equal(("Working", "Idle"), (soon, Row(page, Claude).StateWord));
    }

    [Fact]
    public async Task A_state_that_is_not_estimated_changes_at_once_3082()
    {
        var list = AgentsPageFixture.Edited(_ => { });
        _f.Serve(list);
        var page = await _f.Shown(Token);
        Set(list, Claude, "idle", estimated: false);
        _f.Serve(list);

        await page.Refresh(AgentsPageFixture.Url, Token);

        Assert.Equal("Idle", Row(page, Claude).StateWord);
    }

    [Fact]
    public async Task A_row_with_no_title_is_named_by_repo_and_folder_and_never_by_its_id_3082()
    {
        var list = AgentsPageFixture.Edited(l =>
        {
            AgentsPageFixture.Row(l, Claude)["title"] = null;
            AgentsPageFixture.Row(l, Claude)["repo"] = "web";
            AgentsPageFixture.Row(l, Claude)["worktree"] = "wt/feat-x";
        });
        _f.Serve(list);

        var page = await _f.Shown(Token);

        Assert.Equal("web feat-x", Row(page, Claude).TitleText);
        Assert.DoesNotContain("pid-333", Row(page, "cursor/").TitleText, StringComparison.Ordinal);
        Assert.DoesNotContain("bbbbbbbb", Row(page, Codex).TitleText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ALPHA", "alpha")]
    [InlineData("web", "alpha")]
    [InlineData("ideas-lane", "alpha")]
    [InlineData("wt/alpha-tree", "alpha")]
    public async Task Search_matches_name_repo_lane_and_folder_without_case_3024(string text, string expected)
    {
        var list = AgentsPageFixture.Edited(_ => { });
        var row = Add(list, Claude, "000000000012", title: "alpha", repo: "web", lane: "ideas-lane");
        row["worktree"] = "wt/alpha-tree";
        _f.Serve(list);
        var page = await _f.Shown(Token);

        page.SearchText = text;

        var shown = page.Groups.SelectMany(g => g.Rows).Select(r => r.TitleText).ToList();
        Assert.Equal([expected], shown);
    }

    [Fact]
    public async Task A_search_that_matches_nothing_says_so_and_clearing_it_brings_the_rows_back_3024()
    {
        var page = await _f.Shown(Token);

        page.SearchText = "zzz-nothing";
        var empty = page.IsEmpty;
        page.SearchText = string.Empty;

        Assert.Equal((true, 3), (empty, page.Groups.Sum(g => g.Rows.Count)));
    }

    [Fact]
    public async Task Slash_asks_for_the_search_box_3024()
    {
        var page = await _f.Shown(Token);
        var asked = 0;
        page.SearchRequested += (_, _) => asked++;

        var used = page.Press("/");

        Assert.Equal((true, 1), (used, asked));
    }

    [Fact]
    public async Task Close_and_open_bracket_jump_between_asking_rows_and_wrap_3082()
    {
        var list = AgentsPageFixture.Edited(_ => { });
        _ = Add(list, Codex, "000000000012", title: "zz-asking");
        _f.Serve(list);
        var page = await _f.Shown(Token);
        var asking = page.Groups[0].Rows.Select(r => r.Ref).ToList();

        _ = page.Press("]");
        var one = page.SelectedKey;
        _ = page.Press("]");
        var two = page.SelectedKey;
        _ = page.Press("]");
        var wrapped = page.SelectedKey;
        _ = page.Press("[");
        var back = page.SelectedKey;

        Assert.Equal(2, asking.Count);
        Assert.Equal((asking[0], asking[1], asking[0], asking[1]), (one, two, wrapped, back));
    }

    private static SessionRowViewModel Row(AgentsPageViewModel page, string prefix) => page.Groups.SelectMany(g => g.Rows).Single(r => r.Ref.StartsWith(prefix, StringComparison.Ordinal));

    private static void Set(JsonNode list, string prefix, string state, bool estimated)
    {
        var row = AgentsPageFixture.Row(list, prefix);
        row["state"] = state;
        row["estimated"] = estimated;
        row["stateSource"] = "transcript";
    }

    [Fact]
    public async Task The_head_link_selects_the_first_Needs_you_row_and_asks_for_its_focus_3082()
    {
        var page = await _f.Shown(Token);
        AgentFocusEventArgs? asked = null;
        page.FocusRequested += (_, e) => asked = e;
        var first = page.Groups[0].Rows[0].Ref;

        var used = page.FocusNeedsYou();

        Assert.Equal((true, true, first, first), (used, page.HasAsking, page.SelectedKey, asked?.Key));
    }

    [Fact]
    public async Task The_head_link_does_nothing_when_nobody_asks_3082()
    {
        _f.Serve(AgentsPageFixture.Edited(l => AgentsPageFixture.Remove(l, Codex)));
        var page = await _f.Shown(Token);

        Assert.Equal((false, false, null), (page.FocusNeedsYou(), page.HasAsking, page.SelectedKey));
    }

    [Fact]
    public async Task HasAsking_reads_true_inside_its_own_change_event_when_the_first_asking_row_arrives_3726()
    {
        _f.Serve(AgentsPageFixture.Edited(l => AgentsPageFixture.Remove(l, Codex)));
        var page = await _f.Shown(Token);
        Assert.False(page.HasAsking);
        var seen = new List<bool>();
        page.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(AgentsPageViewModel.HasAsking), StringComparison.Ordinal))
            {
                seen.Add(page.HasAsking);
            }
        };
        _f.Serve(AgentsPageFixture.Edited(_ => { }));   // the same list with its asking row back

        await page.Refresh(AgentsPageFixture.Url, Token);

        Assert.NotEmpty(seen);
        Assert.All(seen, Assert.True);   // a head link that read the old groups would be disabled until the next read
    }

    [Fact]
    public async Task A_row_that_is_asking_with_no_asking_payload_draws_no_blank_reason_line_3726()
    {
        _f.Serve(AgentsPageFixture.Edited(l => AgentsPageFixture.Row(l, Codex)["asking"] = null));

        var page = await _f.Shown(Token);

        var row = page.Groups[0].Rows.Single();
        Assert.Equal((true, string.Empty, false), (row.IsAsking, row.AskingText, row.HasAsking));   // the reason line binds HasAsking, so it stays hidden
        Assert.DoesNotContain(", ,", row.Name, StringComparison.Ordinal);
        Assert.False(row.Name.EndsWith(", ", StringComparison.Ordinal) || row.Name.EndsWith(','));
    }

    [Fact]
    public async Task The_first_click_on_the_Agent_header_sorts_A_to_Z_and_the_second_Z_to_A_3726()
    {
        var list = AgentsPageFixture.Edited(_ => { });
        _ = Add(list, Claude, "000000000011", title: "zeta");
        _ = Add(list, Claude, "000000000012", title: "alpha");
        _f.Serve(list);
        var page = await _f.Shown(Token);

        page.SortBy(AgentSort.Name);
        var first = Titles(Outside(page));
        var firstDescending = page.SortDescending;
        page.SortBy(AgentSort.Name);
        var second = Titles(Outside(page));

        Assert.False(firstDescending);
        Assert.Equal(first.Order(StringComparer.OrdinalIgnoreCase).ToList(), first);
        Assert.Equal(first.AsEnumerable().Reverse().ToList(), second);
        Assert.True(page.SortDescending);
    }

    [Fact]
    public async Task A_header_click_sorts_by_it_and_a_second_click_flips_the_direction_and_the_active_one_is_marked_3082()
    {
        var list = AgentsPageFixture.Edited(_ => { });
        _ = Add(list, Claude, "000000000011", title: "zeta", since: "2026-10-16T10:00:00.0000000Z");
        _ = Add(list, Claude, "000000000012", title: "alpha", since: "2026-10-16T11:00:00.0000000Z");
        _f.Serve(list);
        var page = await _f.Shown(Token);

        page.SortBy(AgentSort.Age);
        var up = Titles(Outside(page));
        var upMark = page.ForHeader;
        page.SortBy(AgentSort.Age);
        var down = Titles(Outside(page));
        var downMark = page.ForHeader;
        await page.Refresh(AgentsPageFixture.Url, Token);
        var afterRead = Titles(Outside(page));
        page.SortBy(AgentSort.Repo);

        Assert.Equal((["zeta", "alpha", "private title"], ["private title", "alpha", "zeta"], ["private title", "alpha", "zeta"]), (up, down, afterRead));
        Assert.Equal((true, false), (upMark.Contains('▲', StringComparison.Ordinal), page.ForHeader.Contains('▲', StringComparison.Ordinal) || page.ForHeader.Contains('▼', StringComparison.Ordinal)));
        Assert.True(downMark.Contains('▼', StringComparison.Ordinal));
        Assert.Equal((AgentSort.Repo, false), (page.Sort, page.SortDescending));
        Assert.Contains('▲', page.RepoHeader, StringComparison.Ordinal);
    }
}
