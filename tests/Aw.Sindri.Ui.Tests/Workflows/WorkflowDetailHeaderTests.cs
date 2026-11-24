using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
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
    public async Task An_active_workflows_page_shows_no_state_chip_and_the_list_shows_none()
    {
        var page = await _f.Shown(Token);
        Assert.False(page.ShowsDetailState);

        await page.OpenWorkflow(Slug, Token);

        Assert.False(page.ShowsDetailState);   // the #3684 frames (canvas-16.png): an active workflow's header has its title and step count only
        page.Back();
        Assert.False(page.ShowsDetailState);
    }

    [Fact]
    public async Task An_off_workflows_page_shows_the_off_chip_beside_its_title()
    {
        var page = await _f.Shown(Token);
        var off = Golden("workflows-slug", "found");
        off["activeRevision"] = null;
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, off.ToJsonString());

        await page.OpenWorkflow(Slug, Token);

        Assert.True(page.ShowsDetailState);   // cards.png: "a paused workflow shows this chip in its header"
        Assert.Equal(Labels.WfState(active: false).Word, page.DetailChipWord, StringComparer.Ordinal);
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
