using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Agents;
using static Aw.Sindri.Ui.Tests.Agents.Page.AgentsPageFixture;

namespace Aw.Sindri.Ui.Tests.Agents.Page;

/// <summary>The Agents page's filter box and column sort (issue 3024): FilterText narrows the one list that was read, and a header click sorts it.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentsPageFilterTests
{
    private const string Claude = "claude-code/";
    private const string Codex = "codex/";
    private const string Cursor = "cursor/";
    private readonly AgentsPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string[] Shown(AgentsPageViewModel page) =>
        [.. page.Groups.SelectMany(g => g.Rows).Select(r => r.Ref[..(r.Ref.IndexOf('/', StringComparison.Ordinal) + 1)])];

    // The golden: Claude Code "private title" in repo api, running, lane sindri/ideas; Codex in repo web, asking (approval, Bash);
    // Cursor with no repo, idle, started by a Sindri launch at Step build.
    [Theory]
    [InlineData("private", Claude)]        // the name
    [InlineData("web", Codex)]             // the repo
    [InlineData("bash", Codex)]            // what it is doing
    [InlineData("p:codex", Codex)]
    [InlineData("p:Claude", Claude)]
    [InlineData("r:api", Claude)]
    [InlineData("s:asking", Codex)]
    [InlineData("s:blocked", Codex)]
    [InlineData("s:working", Claude)]
    [InlineData("s:idle", Cursor)]
    [InlineData("n:private", Claude)]
    [InlineData("o:ideas", Claude)]
    [InlineData("o:agent session", "")]    // a token holds no spaces: "agent" is the o: value, "session" a plain word that no row has
    [InlineData("o:agent", Cursor)]        // started by a Sindri launch
    public async Task Text_narrows_the_list_to_the_rows_it_names(string text, string expected)
    {
        var page = await _f.Shown(Token);

        page.FilterText = text;

        Assert.Equal(expected.Length == 0 ? [] : new[] { expected }, Shown(page), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Tokens_and_words_narrow_together_and_case_is_ignored()
    {
        var page = await _f.Shown(Token);

        page.FilterText = "P:CODEX r:web BASH";
        Assert.Equal([Codex], Shown(page), StringComparer.Ordinal);

        page.FilterText = "p:codex r:api";
        Assert.Empty(Shown(page));
        Assert.True(page.IsEmpty);
        Assert.Equal(Labels.NoSessionsMatch, page.EmptyText);
        Assert.True(page.ShowsClearFilters);
    }

    [Fact]
    public async Task A_token_the_page_does_not_know_is_searched_as_a_word()
    {
        var page = await _f.Shown(Token);

        page.FilterText = "x:foo";

        Assert.Empty(Shown(page));
    }

    [Fact]
    public async Task Clearing_the_text_brings_every_row_back_without_a_new_read()
    {
        var page = await _f.Shown(Token);
        var reads = _f.Server.SessionsCalls;
        var all = Shown(page);

        page.FilterText = "p:codex";
        page.FilterText = string.Empty;

        Assert.Equal(all, Shown(page), StringComparer.Ordinal);
        Assert.Equal(reads, _f.Server.SessionsCalls);
    }

    [Fact]
    public async Task The_head_names_the_shown_agents_while_text_narrows_the_list()
    {
        var page = await _f.Shown(Token);
        var head = page.Head;

        page.FilterText = "p:codex";
        Assert.StartsWith("1 of ", page.Head, StringComparison.Ordinal);

        page.FilterText = string.Empty;
        Assert.Equal(head, page.Head);
    }

    [Fact]
    public async Task Text_set_before_the_first_read_is_applied_when_the_list_arrives()
    {
        var page = _f.Page();
        page.SetShown(shown: true);
        page.FilterText = "s:asking";

        await page.Refresh(Url, Token);

        Assert.Equal([Codex], Shown(page), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Text_stays_through_a_re_read()
    {
        var page = await _f.Shown(Token);
        page.FilterText = "p:codex";

        await page.Refresh(Url, Token);

        Assert.Equal("p:codex", page.FilterText);
        Assert.Equal([Codex], Shown(page), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Clear_filters_empties_the_text()
    {
        var page = await _f.Shown(Token);
        page.FilterText = "p:codex r:api";

        page.ClearFilters();

        Assert.Equal(string.Empty, page.FilterText);
        Assert.Equal(3, Shown(page).Length);
    }

    [Fact]
    public async Task Esc_clears_the_text_and_sends_focus_back_to_the_list()
    {
        var page = await _f.Shown(Token);
        page.FilterText = "p:codex";
        var asked = new List<AgentFocusEventArgs>();
        page.FocusRequested += (_, e) => asked.Add(e);

        var used = page.EscapeFilter();

        Assert.True(used);
        Assert.Equal(string.Empty, page.FilterText);
        var focus = Assert.Single(asked);
        Assert.Equal(page.Groups[0].Rows[0].Ref, focus.Key);
        Assert.False(focus.OnlyIfLost);
    }

    [Fact]
    public async Task Esc_on_an_empty_box_still_gives_the_focus_back()
    {
        var page = await _f.Shown(Token);
        var asked = 0;
        page.FocusRequested += (_, _) => asked++;

        Assert.True(page.EscapeFilter());

        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task The_slash_key_asks_the_view_to_focus_the_filter_box()
    {
        var page = await _f.Shown(Token);
        var asked = 0;
        page.FilterFocusRequested += (_, _) => asked++;

        Assert.True(page.Press("/"));

        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task The_first_click_on_a_header_sorts_it_ascending_and_the_second_reverses_it()
    {
        var page = await _f.Shown(Token);
        Assert.Null(page.SortColumn);

        page.SortBy(AgentSortColumn.Repo);
        Assert.Equal((AgentSortColumn.Repo, false), (page.SortColumn, page.SortDescending));
        Assert.Equal([Claude, Codex], OutsideRows(page), StringComparer.Ordinal);   // api, web

        page.SortBy(AgentSortColumn.Repo);
        Assert.Equal((AgentSortColumn.Repo, true), (page.SortColumn, page.SortDescending));
        Assert.Equal([Codex, Claude], OutsideRows(page), StringComparer.Ordinal);   // web, api
    }

    [Fact]
    public async Task Another_column_starts_ascending_again()
    {
        var page = await _f.Shown(Token);
        page.SortBy(AgentSortColumn.Repo);
        page.SortBy(AgentSortColumn.Repo);

        page.SortBy(AgentSortColumn.Agent);

        Assert.Equal((AgentSortColumn.Agent, false), (page.SortColumn, page.SortDescending));
    }

    [Fact]
    public async Task Status_sorts_the_sessions_that_wait_on_you_first()
    {
        var page = await _f.Shown(Token);

        page.SortBy(AgentSortColumn.Status);

        Assert.Equal([Codex, Claude], OutsideRows(page), StringComparer.Ordinal);   // asking, then running
    }

    [Fact]
    public async Task A_sort_keeps_its_groups_and_stays_through_a_re_read()
    {
        var page = await _f.Shown(Token);
        page.SortBy(AgentSortColumn.Repo);
        page.SortBy(AgentSortColumn.Repo);

        await page.Refresh(Url, Token);

        Assert.Equal([Labels.StartedBySindri, Labels.OutsideWorkflow], page.Groups.Select(g => g.Name), StringComparer.Ordinal);
        Assert.Equal([Codex, Claude], OutsideRows(page), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_sort_and_a_filter_work_together()
    {
        var page = await _f.Shown(Token);
        page.SortBy(AgentSortColumn.Repo);
        page.SortBy(AgentSortColumn.Repo);

        page.FilterText = "r:a";

        Assert.Equal([Claude], OutsideRows(page), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_sorted_header_shows_an_arrow_for_the_direction()
    {
        var page = await _f.Shown(Token);
        Assert.Equal(page.Columns.Repo, page.RepoCaption);

        page.SortBy(AgentSortColumn.Repo);
        Assert.Equal(page.Columns.Repo + " ▲", page.RepoCaption);
        Assert.Equal(page.Columns.Status, page.StatusCaption);

        page.SortBy(AgentSortColumn.Repo);
        Assert.Equal(page.Columns.Repo + " ▼", page.RepoCaption);

        page.SortBy(AgentSortColumn.Status);
        Assert.Equal(page.Columns.Repo, page.RepoCaption);
    }

    private static string[] OutsideRows(AgentsPageViewModel page) =>
        [.. page.Groups.Single(g => string.Equals(g.Name, Labels.OutsideWorkflow, StringComparison.Ordinal)).Rows.Select(r => r.Ref[..(r.Ref.IndexOf('/', StringComparison.Ordinal) + 1)])];
}
