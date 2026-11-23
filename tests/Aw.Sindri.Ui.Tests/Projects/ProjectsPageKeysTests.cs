using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The Projects page's keys (issue 3717 key map) on the mixed golden, one list: 1 to 4 pick a view, O and B ask for the menus, J / K walk the rows with focus following, Enter opens the selected row.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectsPageKeysTests
{
    private readonly ProjectsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private Task<ProjectsPageViewModel> Shown() => _f.Shown(Token, ProjectGrouping.None);

    [Theory]
    [InlineData("1", 1)]
    [InlineData("2", 2)]
    [InlineData("3", 3)]
    [InlineData("4", 4)]
    public async Task A_digit_shows_its_view(string key, int view)   // 1 Needs me, 2 My work, 3 Projects, 4 Triage (the enum is internal)
    {
        var page = await Shown();

        Assert.True(page.Press(key));

        Assert.Equal((ProjectView)view, page.View);
    }

    [Fact]
    public async Task O_and_B_ask_for_the_Sort_and_the_Group_menu()
    {
        var page = await Shown();
        var asked = new List<ProjectMenu>();
        page.MenuRequested += (_, e) => asked.Add(e.Menu);

        Assert.True(page.Press("O"));
        Assert.True(page.Press("B"));

        Assert.Equal([ProjectMenu.Sort, ProjectMenu.Group], asked);
    }

    [Fact]
    public async Task J_and_K_walk_the_rows_stop_at_the_ends_and_focus_follows()
    {
        var page = await Shown();
        var focused = new List<string?>();
        page.FocusRequested += (_, e) => focused.Add(e.CardId);
        var ids = page.Groups[0].Cards.Select(c => c.Id).ToList();

        Assert.True(page.Press("J"));
        Assert.Equal(ids[0], page.SelectedId);
        _ = page.Press("J");
        _ = page.Press("J");
        _ = page.Press("J");
        Assert.Equal(ids[2], page.SelectedId);
        _ = page.Press("K");

        Assert.Equal(ids[1], page.SelectedId);
        Assert.Equal([ids[0], ids[1], ids[2], ids[2], ids[1]], focused);
        Assert.Equal([false, true, false], page.Groups[0].Cards.Select(c => c.IsSelected));
    }

    [Fact]
    public async Task K_from_no_row_goes_to_the_last()
    {
        var page = await Shown();

        _ = page.Press("K");

        Assert.Equal(page.Groups[0].Cards[^1].Id, page.SelectedId);
    }

    [Fact]
    public async Task Enter_opens_the_selected_row_and_does_nothing_without_one()
    {
        var page = await Shown();
        var asked = new List<DeepLink>();
        page.OpenRequested += (_, e) => asked.Add(e.Link);

        Assert.False(page.Press("Enter"));
        var kitchen = page.Groups[0].Cards[1];
        Assert.True(page.Select(kitchen.Id));
        Assert.True(page.Press("Enter"));

        Assert.Equal([kitchen.Link], asked);
    }

    [Fact]
    public async Task A_view_that_drops_the_selected_row_moves_the_selection_to_a_neighbour()
    {
        var page = await Shown();
        var mobile = page.Groups[0].Cards[2];   // attached: not in My work
        Assert.True(page.Select(mobile.Id));

        page.SetView(ProjectView.MyWork);

        Assert.Equal(page.Groups[0].Cards[1].Id, page.SelectedId);   // the row before it, since none followed
        Assert.False(mobile.IsSelected);
    }

    [Fact]
    public async Task A_key_the_page_does_not_use_is_left_to_the_shell()
    {
        var page = await Shown();

        Assert.False(page.Press("C"));
        Assert.False(page.Press("G"));
    }
}
