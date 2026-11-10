using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The New workflow form (#2550): a name and a starter template, Create off with a reason until the name is valid, then the new workflow opens in the editor sheet; a refusal shows Sindri's reason in the form.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsCreateTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<WorkflowsPageViewModel> Opened()
    {
        _f.ServeList(Golden("workflows", "list-empty"));
        var page = await _f.Shown(Token);
        page.OpenCreate();
        return page;
    }

    [Fact]
    public async Task Opening_the_form_offers_the_three_templates_the_server_accepts_with_the_first_picked()
    {
        var page = await Opened();

        Assert.True(page.IsCreating);
        Assert.Equal(["weekday-check", "daily-digest", "manual-notify"], page.Templates.Select(t => t.Name), StringComparer.Ordinal);
        Assert.All(page.Templates, t => Assert.NotEmpty(t.Description));
        Assert.Equal("weekday-check", page.SelectedTemplate);
    }

    // Sindri has no route that lists templates, so the form carries the names GraphTemplates (Aw.Sindri.Core/Graph/Templates/GraphTemplates.cs) accepts.
    // When a template there is renamed, added or removed, this fails: update Labels.WfTemplates with it.
    [Fact]
    public void The_templates_the_form_offers_are_the_three_names_GraphTemplates_accepts()
    {
        Assert.Equal(["weekday-check", "daily-digest", "manual-notify"], Labels.WfTemplates.Select(t => t.Name), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("Morning Check", false)]
    [InlineData("-morning", false)]
    [InlineData("docs", false)]
    [InlineData("blocks", false)]
    [InlineData("morning-check", true)]
    [InlineData("a", true)]
    public async Task Create_is_off_with_a_reason_until_the_name_is_a_valid_slug(string name, bool valid)
    {
        var page = await Opened();

        page.NewSlug = name;

        Assert.Equal(valid, page.CanCreate);
        Assert.Equal(valid, page.CreateReasonText.Length == 0);
    }

    [Fact]
    public async Task A_name_over_63_characters_is_not_valid()
    {
        var page = await Opened();

        page.NewSlug = new string('a', 64);

        Assert.False(page.CanCreate);
        page.NewSlug = new string('a', 63);
        Assert.True(page.CanCreate);
    }

    [Fact]
    public async Task Create_sends_the_name_and_template_once_then_opens_the_new_workflow_in_the_editor_sheet()
    {
        var page = await Opened();
        page.NewSlug = Slug;
        page.SelectedTemplate = "manual-notify";

        await page.Create(Token);

        Assert.Equal([(Slug, "manual-notify")], _f.Server.CreatedWorkflows);
        Assert.Equal((false, true, true, Slug), (page.IsCreating, page.IsRuns, page.IsEditing, page.Editor.Slug));
        Assert.Equal((string.Empty, false), (page.CreateError, page.IsCreateSending));
    }

    [Fact]
    public async Task Create_reads_the_list_again_so_the_new_workflow_is_in_it()
    {
        var page = await Opened();
        page.NewSlug = Slug;
        var reads = _f.Server.WorkflowsCalls;

        await page.Create(Token);

        Assert.True(_f.Server.WorkflowsCalls > reads);
    }

    [Fact]
    public async Task A_refusal_shows_the_servers_reason_in_the_form_and_keeps_the_name_and_opens_nothing()
    {
        var page = await Opened();
        _f.Server.CreateWorkflowAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-GRAPH-009", "Another workflow already has that slug");
        page.NewSlug = Slug;

        await page.Create(Token);

        Assert.Contains("Another workflow already has that slug", page.CreateError, StringComparison.Ordinal);
        Assert.Equal((true, Slug, false, false, false), (page.IsCreating, page.NewSlug, page.IsRuns, page.IsEditing, page.IsCreateSending));
        Assert.True(page.CanCreate);
    }

    [Fact]
    public async Task Typing_a_new_name_clears_the_refusal()
    {
        var page = await Opened();
        _f.Server.CreateWorkflowAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-GRAPH-009", "Another workflow already has that slug");
        page.NewSlug = Slug;
        await page.Create(Token);

        page.NewSlug = "other-name";

        Assert.Equal(string.Empty, page.CreateError);
    }

    [Fact]
    public async Task A_second_press_while_the_create_is_on_the_way_sends_nothing()
    {
        var page = await Opened();
        _f.Server.CreateWorkflowGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        page.NewSlug = Slug;

        var first = page.Create(Token);
        await page.Create(Token);
        _f.Server.CreateWorkflowGate.SetResult();
        await first;

        Assert.Single(_f.Server.CreatedWorkflows);
    }

    [Fact]
    public async Task Create_with_an_invalid_name_sends_nothing()
    {
        var page = await Opened();
        page.NewSlug = "Not Valid";

        await page.Create(Token);

        Assert.Empty(_f.Server.CreatedWorkflows);
    }

    [Fact]
    public async Task Cancel_closes_the_form_and_forgets_the_name_and_the_refusal()
    {
        var page = await Opened();
        _f.Server.CreateWorkflowAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-GRAPH-009", "Another workflow already has that slug");
        page.NewSlug = Slug;
        await page.Create(Token);

        page.CancelCreate();

        Assert.Equal((false, string.Empty, string.Empty), (page.IsCreating, page.NewSlug, page.CreateError));
    }
}
