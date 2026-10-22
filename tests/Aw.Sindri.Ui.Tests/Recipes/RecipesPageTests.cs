using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.Tests.Recipes;

/// <summary>The Recipes page's table, filters, search, keys and the approve link (recipes spec §1, §5): rows in the API's order, counts, and nothing written.</summary>
[Lane(TestLane.Unit)]
public sealed class RecipesPageTests
{
    private readonly RecipesPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_table_lists_every_workflow_in_the_APIs_order_with_its_caption()
    {
        _f.ServeThree();

        var page = await _f.Shown(Token);

        Expect.Sequence(["Build and prove", "Pull request babysitter", "Review loop"], page.Rows.Select(r => r.Name));
        Assert.Equal("3 workflows · 2 approved", page.Caption);
        Assert.False(page.IsReading);
    }

    [Fact]
    public async Task The_filters_count_all_and_those_that_need_approval()
    {
        _f.ServeThree();

        var page = await _f.Shown(Token);

        Assert.Equal(("All 3", "Needs approval 2"), (page.AllText, page.NeedsText));
    }

    [Fact]
    public async Task Needs_approval_shows_the_unapproved_and_the_out_of_date_and_All_shows_everything_again()
    {
        _f.ServeThree();
        var page = await _f.Shown(Token);

        page.ShowNeeds();
        Expect.Sequence(["Pull request babysitter", "Review loop"], page.Rows.Select(r => r.Name));
        Assert.True(page.IsNeedsFilter);

        page.ShowAll();
        Assert.Equal(3, page.Rows.Count);
        Assert.True(page.IsAllFilter);
    }

    [Fact]
    public async Task Search_narrows_by_name_or_id_and_a_search_that_matches_nothing_says_so()
    {
        _f.ServeThree();
        var page = await _f.Shown(Token);

        page.Query = "babysit";
        Expect.Sequence(["Pull request babysitter"], page.Rows.Select(r => r.Name));
        Assert.False(page.IsNoMatch);

        page.Query = "zzz";
        Assert.Empty(page.Rows);
        Assert.Equal((true, "No workflows match"), (page.IsNoMatch, page.NoMatchText));
    }

    [Fact]
    public async Task A_row_asks_the_shell_to_open_its_sheet()
    {
        _f.ServeThree();
        var page = await _f.Shown(Token);
        DeepLink? opened = null;
        page.OpenRequested += (_, e) => opened = e.Link;

        page.RequestOpen(page.Rows[1].Link);

        Assert.Equal(new DeepLink(DeepLinkKind.Recipe, "workflow.pr-babysitter@e204c3b4ff17"), opened);
    }

    [Fact]
    public async Task J_and_K_move_the_selection_and_Enter_opens_the_selected_row()
    {
        _f.ServeThree();
        var page = await _f.Shown(Token);
        DeepLink? opened = null;
        page.OpenRequested += (_, e) => opened = e.Link;

        Assert.True(page.Press("J"));
        Assert.True(page.Press("J"));
        Assert.True(page.Press("J"));
        Assert.True(page.Press("K"));
        Assert.True(page.Press("Enter"));

        Assert.Equal(("workflow.pr-babysitter", true), (page.Selected?.Id, page.Rows[1].IsSelected));
        Assert.Equal(DeepLinkKind.Recipe, opened?.Kind);
        Assert.False(page.Press("X"));
    }

    [Fact]
    public async Task Enter_with_nothing_selected_does_nothing()
    {
        _f.ServeThree();
        var page = await _f.Shown(Token);

        Assert.False(page.Press("Enter"));
    }

    [Fact]
    public async Task Approve_new_workflows_only_asks_to_go_to_setup()
    {
        _f.ServeThree();
        var page = await _f.Shown(Token);
        var asked = 0;
        page.ApproveNewRequested += (_, _) => asked++;

        page.RequestApproveNew();

        Assert.Equal((1, "Approve new workflows"), (asked, page.ApproveNewText));
    }

    [Fact]
    public async Task A_re_read_keeps_the_rows_that_did_not_change_and_the_selection()
    {
        _f.ServeThree();
        var page = await _f.Shown(Token);
        _ = page.Press("J");
        var first = page.Rows[0];

        await page.Refresh(RecipesPageFixture.Url, Token);

        Assert.Same(first, page.Rows[0]);
        Assert.Equal("workflow.phase", page.Selected?.Id);
    }

    [Fact]
    public async Task The_page_does_not_read_while_it_is_not_the_page_shown()
    {
        _f.ServeThree();
        var page = _f.Page();

        await page.Refresh(RecipesPageFixture.Url, Token);

        Assert.Empty(page.Rows);
        Assert.Equal(0, _f.Server.RecipesCalls);
    }
}
