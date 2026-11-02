using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Run;
using Microsoft.Extensions.Time.Testing;
using StateGlyph = Aw.Sindri.Ui.Api.Workflow.StateGlyph;

namespace Aw.Sindri.Ui.Tests.Run;

/// <summary>The run canvas's inspector (F3 spec 3 §5, slice 4): the blocks a selected step shows, their fallbacks, and the actions: Take over only navigates, a protected approve is the terminal copy.</summary>
[Lane(TestLane.Unit)]
public sealed class RunInspectorViewModelTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();
    private readonly List<DeepLink> _links = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RouteAnswer Body<T>(T value) => new(HttpStatusCode.OK, JsonSerializer.Serialize(value, SindriApiClient.Json));

    private static StepRun Running() =>
        new("produce.1", "produce", 1, 3, "running", "agent", A.Noon.AddMinutes(-5), EndedAt: null, DurationMs: null, Outcome: null, Tokens: null, Actor: null);

    private async Task<RunPageViewModel> Opened(StepRun[]? runs = null, FactRow[]? facts = null)
    {
        var line = RunFixtures.Line("plan");
        var child = RunFixtures.Child("plan", ("produce", StateGlyph.Active, false), ("check", StateGlyph.Pending, false)) with { StepRuns = runs ?? [Running()], Facts = facts ?? [] };
        _server.InstanceBody = id => string.Equals(id, line.Id, StringComparison.Ordinal) ? Body(line)
            : string.Equals(id, "WFI-plan", StringComparison.Ordinal) ? Body(child)
            : Body(RunFixtures.Child(id[4..], ("produce", StateGlyph.Done, false)));
        var page = new RunPageViewModel(_server, new CatalogViewModel(new FakeClipboard(), new FakeTimeProvider(A.Noon)), new FakeClipboard(), new FakeTimeProvider(A.Noon));
        page.LinkRequested += (_, e) => _links.Add(e.Link);
        await page.Open(_api, line.Address, line.Id, Token);
        return page;
    }

    private static async Task Until(Func<bool> done)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(TimeSpan.FromSeconds(10));
        while (!done())
        {
            await Task.Delay(10, limit.Token);
        }
    }

    [Fact]
    public async Task Take_over_on_a_running_step_sends_one_takeover_and_shows_the_outcome()
    {
        _server.CommandAnswer = new RouteAnswer(HttpStatusCode.OK, """{"alreadyApplied":false,"viewer":{"kind":"warp","label":"Warp","outcome":"opened"}}""");
        var page = await Opened();
        page.Select("plan/produce");
        var inspector = page.Inspector!;

        inspector.TakeOver();
        await Until(() => inspector.TakeOverLine.Length > 0 && inspector.TakeOverLine != Labels.AttachSending);

        var sent = Assert.Single(_server.CommandCalls);
        Assert.Equal(("WFI-plan", CommandKind.Takeover), (sent.Instance, sent.Body.Kind));
        Assert.Equal(Labels.LaunchOpenedIn("Warp"), inspector.TakeOverLine);
        Assert.Empty(_links);
    }

    [Fact]
    public async Task A_refused_takeover_shows_its_code_in_the_run_inspector()
    {
        _server.CommandAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-WF-041", "That run has moved on. Read the instance again.");
        var page = await Opened();
        page.Select("plan/produce");
        var inspector = page.Inspector!;

        inspector.TakeOver();
        await Until(() => inspector.TakeOverLine.Length > 0 && inspector.TakeOverLine != Labels.AttachSending);

        Assert.Contains("SND-WF-041", inspector.TakeOverLine, StringComparison.Ordinal);
        _ = Assert.Single(_server.CommandCalls);
    }

    [Fact]
    public async Task Nothing_selected_means_no_inspector()
    {
        var page = await Opened();

        Assert.Null(page.Inspector);
        page.Select("plan/produce");
        Assert.NotNull(page.Inspector);
        _ = page.ClearSelection();
        Assert.Null(page.Inspector);
    }

    [Fact]
    public async Task A_selected_step_shows_its_name_its_place_and_its_timeline()
    {
        var page = await Opened();
        page.Select("plan/produce");

        var inspector = page.Inspector!;

        Assert.Equal("Produce", inspector.Title);
        Assert.Equal(Labels.StepOf("Plan", 1, 2), inspector.Place);
        Assert.Equal("Try 1 of 3", Assert.Single(inspector.Timeline).TryText);
    }

    [Fact]
    public async Task A_step_that_has_not_started_says_when_it_starts()
    {
        var page = await Opened();
        page.Select("plan/check");

        var inspector = page.Inspector!;

        Assert.Empty(inspector.Timeline);
        Assert.Equal(Labels.RunNotStartedYet, inspector.TimelineNote);
    }

    [Fact]
    public async Task A_step_that_ran_but_has_no_run_records_shows_the_no_times_copy()
    {
        var page = await Opened(runs: []);
        page.Select("plan/produce");

        Assert.Equal(Labels.RunNoTimes, page.Inspector!.TimelineNote);
    }

    [Fact]
    public async Task Why_is_not_served_so_it_keeps_its_block_with_the_not_served_line()
    {
        var page = await Opened();
        page.Select("plan/produce");

        Assert.Equal(Labels.RunWhyNotServed, page.Inspector!.WhyText);
    }

    [Fact]
    public async Task Why_is_the_stages_served_why_when_the_recipe_serves_one()
    {
        _server.RecipeBody = _ => new RouteAnswer(HttpStatusCode.OK, RecipeWithWhy("plan", "Turns the idea into a plan you can approve."));
        var page = await Opened();

        page.Select("plan/produce");

        Assert.Equal("Turns the idea into a plan you can approve.", page.Inspector!.WhyText);
    }

    [Fact]
    public async Task A_stage_whose_recipe_state_has_an_empty_why_keeps_the_not_served_line()
    {
        _server.RecipeBody = _ => new RouteAnswer(HttpStatusCode.OK, RecipeWithWhy("plan", "  "));
        var page = await Opened();

        page.Select("plan/produce");

        Assert.Equal(Labels.RunWhyNotServed, page.Inspector!.WhyText);
    }

    // The f5-fields recipe golden with one more state, "plan", carrying the why (or none).
    private static string RecipeWithWhy(string state, string why)
    {
        var recipe = System.Text.Json.Nodes.JsonNode.Parse(GoldenFiles.Api("workflow-recipe", "workflow.f5-fields"))!;
        var added = System.Text.Json.Nodes.JsonNode.Parse(recipe["states"]![0]!.ToJsonString())!;
        added["id"] = state;
        added["why"] = why;
        recipe["states"]!.AsArray().Add(added);
        return recipe.ToJsonString();
    }

    [Fact]
    public async Task Facts_are_headed_for_the_whole_stage_and_split_by_direction()
    {
        var page = await Opened(facts: [Fact("brief", FactDirection.Input, FactStatus.Produced), Fact("plan", FactDirection.Output, FactStatus.Pending)]);
        page.Select("plan/produce");

        var inspector = page.Inspector!;

        Assert.Equal(Labels.RunFactsWholeStage, inspector.FactsHeading);
        Assert.Equal(Labels.FactReady, Assert.Single(inspector.Needs).Word);
        Assert.Equal(Labels.FactNotProduced, Assert.Single(inspector.Produced).Word);
    }

    [Fact]
    public async Task See_its_context_opens_the_items_map()
    {
        var page = await Opened();
        page.Select("plan/produce");

        page.Inspector!.SeeContext();

        Assert.Equal(DeepLinkKind.ContextItem, Assert.Single(_links).Kind);
    }

    [Fact]
    public async Task Close_clears_the_selection_and_previous_and_next_walk_the_steps()
    {
        var page = await Opened();
        page.Select("plan/produce");

        page.Inspector!.Next();
        Assert.Equal("plan/check", page.SelectedKey);
        page.Inspector!.Previous();
        Assert.Equal("plan/produce", page.SelectedKey);
        page.Inspector!.Close();
        Assert.Null(page.SelectedKey);
    }

    private static FactRow Fact(string key, FactDirection direction, FactStatus status) =>
        new(key, key, FactType.Text, direction, From: null, status, Summary: "A summary", At: null, By: null, Ref: null, Progress: null, Pending: null);
}
