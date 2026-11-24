using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Projects;
using static Aw.Sindri.Ui.Tests.Projects.ProjectsFixture;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>
/// The Projects page's search, folding group heads and the Needs me ask (issue 3717, frames round 13 search, search-none, needs-me) on the mixed golden:
/// Checkout rewrite (led), Kitchen (by hand), Mobile app (attached, note "Waiting on the vendor").
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ProjectsPageSearchTests
{
    private readonly ProjectsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static List<string> Rows(ProjectsPageViewModel page) => [.. page.Groups.Where(g => g.IsExpanded).SelectMany(g => g.Cards).Select(c => c.Name)];

    [Fact]
    public async Task Search_keeps_the_projects_whose_name_holds_the_words_in_any_case()
    {
        var page = await _f.Shown(Token, ProjectGrouping.None);

        page.SearchText = "KITCHEN";

        Assert.Equal(["Kitchen"], Rows(page), StringComparer.Ordinal);
        Assert.Equal(("1 of 3", false), (page.SearchCount, page.Groups[0].Cards[0].MatchedInNote));
        Assert.Equal(1, _f.Server.ProjectsCalls);   // it filters what was served
    }

    [Fact]
    public async Task A_project_found_only_by_its_note_says_so()
    {
        var page = await _f.Shown(Token, ProjectGrouping.None);

        page.SearchText = "vendor";

        var row = Assert.Single(page.Groups[0].Cards);
        Assert.Equal(("Mobile app", true, "matched in note"), (row.Name, row.MatchedInNote, row.MatchedInNoteText));
    }

    [Fact]
    public async Task No_match_shows_its_empty_state_and_Clear_search_brings_the_rows_back()
    {
        var page = await _f.Shown(Token, ProjectGrouping.None);

        page.SearchText = "xyz";

        Assert.Empty(page.Groups);
        Assert.Equal((true, "No project matches “xyz”", "0 of 3"), (page.IsSearchEmpty, page.SearchEmptyTitle, page.SearchCount));
        Assert.True(page.ClearSearch());
        Assert.Equal((false, string.Empty, 3), (page.IsSearchEmpty, page.SearchCount, Rows(page).Count));
        Assert.False(page.ClearSearch());   // nothing left to clear: Esc goes on to the shell
    }

    [Fact]
    public async Task Slash_asks_for_the_search_field_and_the_field_shows_its_own_keys()
    {
        var page = await _f.Shown(Token, ProjectGrouping.None);
        var asked = 0;
        page.SearchRequested += (_, _) => asked++;

        Assert.True(page.Press("/"));
        page.SearchFocused(focused: true);

        Assert.Equal(1, asked);
        Assert.Equal(["Esc", "↓", "Enter"], page.KeyHints.Select(h => h.Key), StringComparer.Ordinal);
        page.SearchFocused(focused: false);
        Assert.Contains(page.KeyHints, h => h.Key == "j/k");
    }

    [Fact]
    public async Task A_folded_group_hides_its_rows_from_the_keys_and_stays_folded_across_reads()
    {
        var page = await _f.Shown(Token);   // by state: In progress, Planned, Paused, one project each
        Assert.True(page.Select(page.Groups[0].Cards[0].Id));

        page.ToggleGroup(page.Groups[0]);

        Assert.Equal((true, "▸"), (page.Groups[0].IsCollapsed, page.Groups[0].Chevron));
        Assert.Equal(page.Groups[1].Cards[0].Id, page.SelectedId);   // the selection moves to the next row drawn
        Assert.Equal(["Kitchen", "Mobile app"], Rows(page), StringComparer.Ordinal);
        Assert.EndsWith(", collapsed", page.Groups[0].HeadingName, StringComparison.Ordinal);

        await page.Refresh(Url, Token);

        Assert.True(page.Groups[0].IsCollapsed);
    }

    [Fact]
    public async Task On_Needs_me_a_project_waiting_on_you_reads_you_review()
    {
        var list = Golden("mixed");
        list["projects"]![1]!["truth"] = new JsonObject
        {
            ["openPrs"] = new JsonArray(),
            ["openItems"] = 0,
            ["doneItems"] = 0,
            ["totalItems"] = 0,
            ["runningAgents"] = 0,
            ["health"] = null,
            ["blockedOnYou"] = true,
        };
        _f.Serve(list);
        var page = await _f.Shown(Token, ProjectGrouping.None, defaultView: true);   // opens on Needs me: the flag is served
        var kitchen = page.Groups[0].Cards[0];

        Assert.Equal(("Kitchen", "you · review"), (kitchen.Name, kitchen.BlockedOnCell));
        page.SetView(ProjectView.All);
        Assert.Equal("you", kitchen.BlockedOnCell);
    }
}
