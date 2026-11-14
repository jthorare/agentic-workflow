using Aw.Kernel.Testing;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The header of one workflow's page (#2807): the list's counts belong to the list, the detail shows this workflow's own state, and Edit hides while edit mode is on.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowDetailHeaderTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_counts_show_on_the_list_and_not_on_a_workflows_page()
    {
        var page = await _f.Shown(Token);
        Assert.True(page.ShowsListHead);

        await page.OpenWorkflow(Slug, Token);

        Assert.False(page.ShowsListHead);
        page.Back();
        Assert.True(page.ShowsListHead);
    }

    [Fact]
    public async Task A_workflows_page_shows_its_own_state_word_and_the_list_does_not()
    {
        var page = await _f.Shown(Token);
        Assert.False(page.ShowsDetailState);

        await page.OpenWorkflow(Slug, Token);

        Assert.True(page.ShowsDetailState);
        Assert.Equal(page.Rows[0].ChipGlyph + " " + page.Rows[0].ChipWord, page.DetailStateText);
        page.Back();
        Assert.False(page.ShowsDetailState);
    }

    [Fact]
    public async Task Edit_hides_while_edit_mode_is_on_and_comes_back_on_Done()
    {
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        Assert.True(page.ShowsEdit);

        page.Edit();
        Assert.False(page.ShowsEdit);

        page.CloseEditor();
        Assert.True(page.ShowsEdit);
    }
}
