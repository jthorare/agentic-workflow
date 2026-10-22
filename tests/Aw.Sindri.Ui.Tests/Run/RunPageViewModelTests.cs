using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Run;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Run;

/// <summary>The run page's view model (F3 spec 3, slice 3): it opens a line run on its active stage at 100 %, lists the runs of the same workflow, walks the steps with J and K and rereads on a change of the run or of a stage's child.</summary>
[Lane(TestLane.Unit)]
public sealed class RunPageViewModelTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();
    private readonly List<string> _reads = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RouteAnswer Body<T>(T value) => new(HttpStatusCode.OK, JsonSerializer.Serialize(value, SindriApiClient.Json));

    // The line at a stage, its stages' children with two steps each, every read recorded by instance id.
    private void Serve(InstanceDetail line)
    {
        var children = RunFixtures.Stages.ToDictionary(
            s => "WFI-" + s,
            s => RunFixtures.Child(s, ("produce", StateGlyph.Active, false), ("check", StateGlyph.Pending, false)),
            StringComparer.Ordinal);
        _server.InstanceBody = id =>
        {
            _reads.Add(id);
            return string.Equals(id, line.Id, StringComparison.Ordinal) ? Body(line) : children.TryGetValue(id, out var child) ? Body(child) : null;
        };
    }

    private RunPageViewModel Page() =>
        new(_server, new CatalogViewModel(new FakeClipboard(), new FakeTimeProvider(A.Noon)), new FakeClipboard(), new FakeTimeProvider(A.Noon));

    private async Task<RunPageViewModel> Opened(string at = "plan")
    {
        var line = RunFixtures.Line(at);
        Serve(line);
        var page = Page();
        await page.Open(_api, line.Address, line.Id, Token);
        return page;
    }

    [Fact]
    public async Task Opens_at_the_active_stage_at_100_percent()
    {
        var page = await Opened("plan");
        page.SetViewport(1000, 700);

        var focus = page.FocusActive();

        Assert.Equal(RunPageState.Shown, page.Phase);
        Assert.Equal("plan", page.ActiveNodeId);
        var plan = page.Layout.Rects["plan"];
        Assert.Equal(1, focus.Scale);
        Assert.Equal(500, focus.X);   // the stage is centred across the visible width
        Assert.Equal(Math.Max(84, (700 - plan.Height) / 2) - plan.Y, focus.Y);
        Assert.Equal(focus, page.Opening);
    }

    [Fact]
    public async Task A_stage_size_that_arrives_after_the_read_opens_the_canvas_then()
    {
        var page = await Opened("design");
        RunFocus? asked = null;
        page.ViewRequested += (_, e) => asked = e.Value;

        Assert.Null(page.Opening);
        page.SetViewport(1200, 800, rightInset: 480);

        _ = Assert.NotNull(asked);
        Assert.Equal(1, asked.Value.Scale);
        Assert.Equal(360, asked.Value.X);   // (1200 - 480) / 2 around the centre column
    }

    [Fact]
    public async Task The_cards_are_the_stages_in_order_with_their_steps()
    {
        var page = await Opened("plan");

        Assert.Equal(["scoping", "design", "plan", "phase"], page.Cards.Select(c => c.Id), StringComparer.Ordinal);
        var plan = page.Cards.Single(c => c.Id == "plan");
        Assert.Equal(["produce", "check"], plan.Steps.Select(s => s.Name.ToLowerInvariant()), StringComparer.Ordinal);
        Assert.True(plan.IsActive);
        Assert.Equal("Running", plan.Word);
        Assert.DoesNotContain(page.Cards, c => c.Node.Kind == RunNodeKind.End);   // no end card while the run goes
    }

    [Fact]
    public async Task The_runs_list_asks_for_the_open_runs_of_the_same_workflow()
    {
        _ = await Opened("plan");

        Assert.Equal(new InstanceQuery(Root: true, Open: true, Recipe: "workflow.line"), _server.LastInstancesQuery);
    }

    [Fact]
    public async Task J_and_K_walk_the_steps_in_reading_order_and_Esc_clears()
    {
        var page = await Opened("plan");

        Assert.True(page.Press("J"));
        Assert.Equal("scoping/produce", page.SelectedKey);
        Assert.True(page.Press("J"));
        Assert.Equal("scoping/check", page.SelectedKey);
        Assert.True(page.Press("K"));
        Assert.Equal("scoping/produce", page.SelectedKey);
        Assert.True(page.Cards.Single(c => c.Id == "scoping").Steps[0].IsSelected);
        Assert.True(page.Press("Escape"));
        Assert.Null(page.SelectedKey);
        Assert.False(page.Press("Escape"));   // nothing left to clear
    }

    [Fact]
    public async Task A_change_of_a_stages_child_rereads_the_run_and_another_run_is_left_alone()
    {
        var page = await Opened("plan");
        var child = RunFixtures.Child("plan").Address;
        _reads.Clear();

        Assert.True(await page.Catch(_api, new InstanceChange("ITM-OTHER/line/1", "plan", 10), Token));
        Assert.Empty(_reads);

        _ = await page.Catch(_api, new InstanceChange(child, "produce", 11), Token);
        Assert.Contains("WFI-line", _reads, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_run_that_is_not_served_ends_in_a_problem_with_its_sentence()
    {
        var page = Page();

        await page.Open(_api, "ITM-NONE/line/1", "WFI-none", Token);

        Assert.Equal(RunPageState.NotFound, page.Phase);
        Assert.NotEmpty(page.ProblemText);
        Assert.Empty(page.Cards);
    }

    [Fact]
    public async Task The_header_badge_reads_the_active_stages_word()
    {
        var page = await Opened("plan");

        Assert.Equal("Running", page.HeaderWord);
        Assert.Equal("Brush.PrimaryHover", page.HeaderBrush);
    }
}
