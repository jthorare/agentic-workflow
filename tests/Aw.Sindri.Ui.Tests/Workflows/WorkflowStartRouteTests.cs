using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflows;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The start screen reads GET /sindri/workflows/templates (spec 4.1, 4.8): cards from the route, the app's own A, B and C while the route is not built, and a failed line with Retry.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowStartRouteTests
{
#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
    private const string Templates = """
        {"templates":[
          {"id":"A","title":"Review every PR","trigger":"github.pull_request.opened","steps":3,"needs":["connection:github"],"lastRunAt":null},
          {"id":"B","title":"Plan a labelled issue","trigger":"github.issue.labeled","steps":1,"needs":["connection:github"],"lastRunAt":"2026-01-01T00:03:00Z"},
          {"id":"C","title":"Watch main, open an issue on red","trigger":"schedule","steps":4,"needs":[],"lastRunAt":null}
        ]}
        """;
#pragma warning restore MA0136

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<WorkflowsPageViewModel> OpenForm()
    {
        var page = await _f.Shown(Token);
        page.OpenCreate();
        await page.TemplatesRead;
        return page;
    }

    [Fact]
    public async Task Opening_the_form_reads_the_templates_once_and_the_cards_are_the_routes()
    {
        _f.Server.WorkflowTemplatesAnswer = new RouteAnswer(HttpStatusCode.OK, Templates);

        var page = await OpenForm();

        Assert.Equal(1, _f.Server.WorkflowTemplatesCalls);
        Assert.Equal(["Review every PR", "Plan a labelled issue", "Watch main, open an issue on red", "Blank"], page.Start.Entries.Select(e => e.Title), StringComparer.Ordinal);
        Assert.Equal(["A", "B", "C", string.Empty], page.Start.Entries.Select(e => e.Name), StringComparer.Ordinal);
        Assert.Equal(["A", "B", "C", string.Empty], page.Start.Entries.Select(e => e.Letter), StringComparer.Ordinal);
        Assert.False(page.TemplatesFailed);
    }

    [Fact]
    public async Task A_card_shows_its_trigger_step_count_needs_and_when_it_last_ran()
    {
        _f.Server.WorkflowTemplatesAnswer = new RouteAnswer(HttpStatusCode.OK, Templates);

        var cards = (await OpenForm()).Start.Entries.Take(3).ToList();

        Assert.Equal(["github.pull_request.opened", "github.issue.labeled", "Schedule"], cards.Select(c => c.Trigger), StringComparer.Ordinal);
        Assert.Equal(["3 steps", "1 step", "4 steps"], cards.Select(c => c.Steps), StringComparer.Ordinal);
        Assert.Equal(["GitHub connection", "GitHub connection", "nothing"], cards.Select(c => c.Needs), StringComparer.Ordinal);
        Assert.Equal(["never on this box", "last ran " + Labels.AgoWords(TimeSpan.FromMinutes(2)), "never on this box"], cards.Select(c => c.LastRan), StringComparer.Ordinal);
    }

    [Fact]
    public async Task While_the_route_is_not_built_the_app_keeps_its_own_cards_and_says_nothing_is_wrong()
    {
        var page = await OpenForm();   // the fake answers SND-GRAPH-113 until the engine builds the route

        Assert.Equal(["Review every PR", "Plan a labelled issue", "Watch main, open an issue on red", "Blank"], page.Start.Entries.Select(e => e.Title), StringComparer.Ordinal);
        Assert.False(page.TemplatesFailed);
        Assert.Equal(string.Empty, page.TemplatesFailedText);
    }

    [Fact]
    public async Task A_failed_read_keeps_the_cards_shows_the_failed_line_and_Retry_reads_again()
    {
        _f.Server.WorkflowTemplatesAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-UI-099", "boom");
        var page = await OpenForm();

        Assert.True(page.TemplatesFailed);
        Assert.Equal("Couldn't load templates. Retry", page.TemplatesFailedText);
        Assert.Equal(4, page.Start.Entries.Count);

        _f.Server.WorkflowTemplatesAnswer = new RouteAnswer(HttpStatusCode.OK, Templates);
        await page.RetryTemplates(Token);

        Assert.False(page.TemplatesFailed);
        Assert.Equal(2, _f.Server.WorkflowTemplatesCalls);
        Assert.Equal("github.pull_request.opened", page.Start.Entries[0].Trigger);
    }

    [Fact]
    public async Task Choosing_a_card_makes_Create_send_that_template()
    {
        _f.Server.WorkflowTemplatesAnswer = new RouteAnswer(HttpStatusCode.OK, Templates);
        var page = await OpenForm();

        page.ChooseStart(page.Start.Entries[1]);
        page.NewName = "Plan";
        await page.Create(Token);

        Assert.Equal([("plan", "B")], _f.Server.CreatedWorkflows);
    }

    [Fact]
    public async Task Choosing_Blank_creates_a_workflow_from_the_blank_template()
    {
        var page = await OpenForm();

        page.ChooseStart(page.Start.Entries[^1]);
        page.NewName = "Empty";
        await page.Create(Token);

        Assert.Equal([("empty", "blank")], _f.Server.CreatedWorkflows);
    }

    [Fact]
    public void The_screen_maps_the_routes_templates_then_Blank_and_keeps_the_arrow_keys()
    {
        var list = new WorkflowTemplateList([new WorkflowTemplateView("A", "Review every PR", "manual", 2, [], LastRunAt: null)]);

        var screen = new StartScreenViewModel(list, new Microsoft.Extensions.Time.Testing.FakeTimeProvider(Now));
        screen.Move(StartMove.Down);

        Assert.Equal(["Review every PR", "Blank"], screen.Entries.Select(e => e.Title), StringComparer.Ordinal);
        Assert.True(screen.Selected.IsBlank);
    }
}
