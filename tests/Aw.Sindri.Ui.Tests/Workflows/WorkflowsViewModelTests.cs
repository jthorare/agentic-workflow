using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The Workflows page's list (screens §4.2, U8 T1): the open root rows grouped by recipe in the order the API serves them, the root-wide head, the finished line, the filters and the banners.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsViewModelTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonArray Served(string scenario) => GoldenFiles.Node("workflow-instances", scenario)["instances"]!.AsArray();

    private static string Text(JsonNode? node) => (string)node!;

    private static string Recipe(JsonNode row) => Text(row["recipe"]!["id"]);

    // The recipes in the order the golden first names them, each with the rows it holds, in the golden's order.
    private static List<(string Recipe, string[] Addresses)> Expected(string scenario) =>
        [.. Served(scenario).GroupBy(r => Recipe(r!), StringComparer.Ordinal).Select(g => (g.Key, g.Select(r => Text(r!["address"])).ToArray()))];

    private async Task<WorkflowsViewModel> Shown()
    {
        var list = new WorkflowsViewModel(_server, new FakeClipboard(), new FakeTimeProvider(A.Noon));
        list.SetViewMode(WorkflowViewMode.List);   // the grouped list; the board has its own tests
        await list.Refresh(_api, Token);
        return list;
    }

    private static string[] Addresses(RecipeGroupViewModel group) => [.. group.Rows.Select(r => r.Address)];

    [Fact]
    public async Task Default_reads_root_open()
    {
        var list = await Shown();

        Assert.Equal(new InstanceQuery(Root: true, Open: true), _server.LastInstancesQuery);
        Expect.Sequence(Served("open").Select(r => Text(r!["address"])).Order(StringComparer.Ordinal), list.Groups.SelectMany(Addresses).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Groups_appear_in_first_served_order_and_keep_row_order()
    {
        var list = await Shown();

        var expected = Expected("open");
        Expect.Sequence(expected.Select(g => g.Recipe), list.Groups.Select(g => g.Recipe));
        Assert.All(expected.Zip(list.Groups), pair => Expect.Sequence(pair.First.Addresses, Addresses(pair.Second)));

        // A group holds its rows even when the API served other recipes between them (the triage rows are not adjacent).
        var served = Served("open").Select(r => Recipe(r!)).ToList();
        var split = expected.First(g => g.Addresses.Length > 1).Recipe;
        Assert.True(served.LastIndexOf(split) - served.IndexOf(split) >= expected.First(g => g.Recipe == split).Addresses.Length);
    }

    [Fact]
    public async Task A_group_is_named_for_its_workflow_and_counts_runs_and_never_shows_the_recipe_id()
    {
        var list = await Shown();
        var served = GoldenFiles.Node("workflow-instances", "open")["instances"]!.AsArray()   // why: the golden is a JSON document
            .DistinctBy(static i => (string)i!["recipe"]!["id"]!, StringComparer.Ordinal)
            .ToDictionary(static i => (string)i!["recipe"]!["id"]!, static i => (string)i!["recipe"]!["displayName"]!, StringComparer.Ordinal);

        Assert.All(list.Groups, group =>
        {
            Assert.Equal(served[group.Recipe], group.Name);   // the name the server serves; an older one serves none and the id is made readable (WorkflowNames)
            Assert.Equal(group.Rows.Count == 1 ? "1 run" : $"{group.Rows.Count} runs", group.CountText);
            Assert.Equal($"{group.Name} · {group.CountText}", group.Header);
            Assert.DoesNotContain("workflow.", group.Header, StringComparison.Ordinal);
            Assert.DoesNotContain("workflow.", group.ToggleName, StringComparison.Ordinal);
        });
        Assert.Contains(list.Groups, g => g.Rows.Count > 1);
    }

    [Fact]
    public async Task A_served_display_name_names_the_group()
    {
        var body = GoldenFiles.Node("workflow-instances", "open");
        var first = body["instances"]!.AsArray()[0]!;
        first["recipe"]!["displayName"] = "Build and ship a change";
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());

        var list = await Shown();

        Assert.Equal("Build and ship a change", list.Groups.First(g => g.Recipe == Recipe(first)).Name);
    }

    [Fact]
    public async Task The_workflow_filter_lists_workflows_by_name_and_maps_a_choice_back_to_its_id()
    {
        var list = await Shown();

        Expect.Sequence([Labels.AllRecipes, .. list.Groups.Select(g => g.Name)], list.RecipeChoices);
        Assert.All(list.Groups, g => Assert.Equal(g.Recipe, list.RecipeIdOf(g.Name)));
        Assert.Null(list.RecipeIdOf(Labels.AllRecipes));
        Assert.Equal(("All workflow types", "Workflow type", "Item"), (Labels.AllRecipes, Labels.RecipeFilter, Labels.SubjectFilter));
    }

    [Fact]
    public async Task Two_workflows_with_one_display_name_are_told_apart_in_the_filter_and_map_back_exactly()
    {
        var body = GoldenFiles.Node("workflow-instances", "open");
        var rows = body["instances"]!.AsArray();
        var ids = rows.Select(r => Recipe(r!)).Distinct(StringComparer.Ordinal).Take(2).ToList();
        Assert.Equal(2, ids.Count);
        foreach (var row in rows)
        {
            row!["recipe"]!["displayName"] = "Same name";
        }

        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());

        var list = await Shown();

        Assert.Equal(list.RecipeChoices.Count, list.RecipeChoices.Distinct(StringComparer.Ordinal).Count());
        Assert.All(list.RecipeChoices, c => Assert.DoesNotContain("workflow.", c, StringComparison.Ordinal));
        Assert.All(list.Groups, g => Assert.Equal(g.Recipe, list.RecipeIdOf(list.RecipeChoices[list.Groups.IndexOf(g) + 1])));
    }

    [Fact]
    public async Task Head_reads_open_need_you_with_you_from_counts()
    {
        var list = await Shown();

        var counts = GoldenFiles.Node("workflow-instances", "open")["counts"]!;
        Assert.Equal(Labels.WorkflowsHead(counts["open"]!.ToJsonString(), counts["needsYou"]!.ToJsonString(), counts["withYou"]!.ToJsonString()), list.Head);
        Assert.Equal(counts["open"]!.ToJsonString(), list.NavCount);
    }

    [Fact]
    public async Task Done_line_reads_counts_done_and_expands_to_the_root_done_rows()
    {
        var list = await Shown();

        var done = GoldenFiles.Node("workflow-instances", "root")["counts"]!["done"];
        Assert.Equal((true, $"{done} finished", false, 0), (list.HasDone, list.DoneText, list.IsDoneExpanded, list.DoneRows.Count));

        await list.ToggleDone(Token);

        var finished = Served("root").Where(r => r!["states"]!.AsArray().Any(s => Text(s!["id"]) == Text(r["state"]) && (bool)s["terminal"]!)).Select(r => Text(r!["address"]));
        Assert.True(list.IsDoneExpanded);
        Expect.Sequence(finished, list.DoneRows.Select(r => r.Address));
        Assert.Equal(new InstanceQuery(Root: true, Open: false), _server.LastInstancesQuery);

        await list.ToggleDone(Token);
        Assert.Equal((false, 0), (list.IsDoneExpanded, list.DoneRows.Count));
    }

    [Fact]
    public async Task Recipe_and_Subject_filters_send_recipe_and_subject()
    {
        var list = await Shown();
        var row = Served("recipe-filter")[0]!;

        await list.FilterRecipe(Recipe(row), Token);
        Assert.Equal(new InstanceQuery(Root: true, Open: true, Recipe: Recipe(row)), _server.LastInstancesQuery);

        await list.FilterSubject(Text(row["subject"]!["key"]), Token);
        Assert.Equal(new InstanceQuery(Root: true, Open: true, Recipe: Recipe(row), Subject: Text(row["subject"]!["key"])), _server.LastInstancesQuery);

        await list.FilterRecipe(recipe: null, Token);
        Assert.Equal(new InstanceQuery(Root: true, Open: true, Subject: Text(row["subject"]!["key"])), _server.LastInstancesQuery);
    }

    [Fact]
    public async Task Counts_stay_root_wide_under_a_filter()
    {
        var list = await Shown();
        var head = list.Head;
        var recipe = Recipe(Served("recipe-filter")[0]!);

        await list.FilterRecipe(recipe, Token);

        Assert.All(list.Groups, g => Assert.Equal(recipe, g.Recipe));
        Assert.True(list.Groups.Count < Expected("open").Count);
        Assert.Equal(head, list.Head);
        var counts = GoldenFiles.Node("workflow-instances", "recipe-filter")["counts"]!;
        Assert.Equal(Labels.WorkflowsHead(counts["open"]!.ToJsonString(), counts["needsYou"]!.ToJsonString(), counts["withYou"]!.ToJsonString()), list.Head);
    }

    [Fact]
    public async Task Collapsed_groups_show_count_and_show_all()
    {
        var list = await Shown();
        var group = list.Groups[0];
        var rows = group.Rows.Count;
        Assert.Equal((true, string.Empty, rows), (group.IsExpanded, group.ShowAllText, group.Count));

        group.Toggle();

        Assert.Equal((false, Labels.ShowAll, rows), (group.IsExpanded, group.ShowAllText, group.Count));
        await list.Refresh(_api, Token);
        Assert.Equal((false, rows), (list.Groups[0].IsExpanded, list.Groups[0].Count));
        list.Groups[0].Toggle();
        Assert.True(list.Groups[0].IsExpanded);
    }

    [Fact]
    public async Task Empty_shows_the_served_sentence()
    {
        _server.WorkflowAnswer = RouteAnswer.Api("workflow-instances", "empty");

        var list = await Shown();

        Assert.Equal((true, Text(GoldenFiles.Node("workflow-instances", "empty")["empty"]), 0, false), (list.IsEmpty, list.EmptyText, list.Groups.Count, list.HasDone));
    }

    [Fact]
    public async Task Stalled_and_rebuilding_keep_rows_marked_stale()
    {
        var list = await Shown();
        Assert.All(list.Groups.SelectMany(g => g.Rows), r => Assert.False(r.IsStale));

        list.SetStale(stale: true);
        Assert.All(list.Groups.SelectMany(g => g.Rows), r => Assert.True(r.IsStale));

        await list.Refresh(_api, Token);
        Assert.All(list.Groups.SelectMany(g => g.Rows), r => Assert.True(r.IsStale));

        list.SetStale(stale: false);
        Assert.All(list.Groups.SelectMany(g => g.Rows), r => Assert.False(r.IsStale));
    }

    [Fact]
    public async Task An_unreadable_list_shows_the_shared_unreadable_state()
    {
        var body = (JsonObject)GoldenFiles.Node("workflow-instances", "open");
        _ = body.Remove("counts");   // the engine's shape before API P11 has no counts (N1)
        var list = await Shown();
        var (rowsBefore, headBefore) = (list.Groups.Sum(g => g.Rows.Count), list.Head);   // the last good data stays, marked stale (screens §4.1)
        Assert.False(list.IsUnreadable);

        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
        await list.Refresh(_api, Token);

        Assert.Equal((true, Labels.StripUnreadable, rowsBefore, headBefore, true), (list.IsUnreadable, list.UnreadableText, list.Groups.Sum(g => g.Rows.Count), list.Head, list.Groups.SelectMany(g => g.Rows).All(r => r.IsStale)));

        _server.WorkflowAnswer = null;
        await list.Refresh(_api, Token);
        Assert.Equal((false, true, false), (list.IsUnreadable, list.Groups.Count > 0, list.Groups.SelectMany(g => g.Rows).Any(r => r.IsStale)));
    }

    [Fact]
    public async Task Recipe_options_survive_a_filter()
    {
        var list = await Shown();
        var all = list.Groups.Select(g => g.Recipe).ToList();

        await list.FilterRecipe(all[1], Token);

        Expect.Sequence(all, list.KnownRecipes);
        Assert.Equal(all[1], list.RecipeFilter);
    }

    // A read the server answers late: its gate is set after a newer read has finished.
    private TaskCompletionSource Hold(InstanceQuery query)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _server.InstancesGates[query] = gate;
        return gate;
    }

    [Fact]
    public Task An_older_unfiltered_answer_arriving_after_a_filtered_one_is_dropped() => UiThread.Run(async () =>
    {
        var list = await Shown();
        var recipe = Recipe(Served("recipe-filter")[0]!);
        var late = Hold(new InstanceQuery(Root: true, Open: true));
        var older = list.Refresh(_api, Token);

        await list.FilterRecipe(recipe, Token);
        _server.InstancesGates.Clear();
        late.SetResult();
        await older;

        Assert.All(list.Groups, g => Assert.Equal(recipe, g.Recipe));
        Assert.Equal(recipe, list.RecipeFilter);
    }, Token);

    [Fact]
    public Task A_late_recipe_answer_after_All_recipes_does_not_shrink_the_known_recipes() => UiThread.Run(async () =>
    {
        var list = await Shown();
        var all = list.KnownRecipes.ToList();
        var recipe = Recipe(Served("recipe-filter")[0]!);
        var late = Hold(new InstanceQuery(Root: true, Open: true, Recipe: recipe));
        var narrowing = list.FilterRecipe(recipe, Token);

        await list.FilterRecipe(recipe: null, Token);
        late.SetResult();
        await narrowing;

        Expect.Sequence(all, list.KnownRecipes);
        Expect.Sequence(all, list.Groups.Select(g => g.Recipe));
        Assert.Null(list.RecipeFilter);
    }, Token);

    [Fact]
    public Task Expanding_then_collapsing_the_finished_line_does_not_refill_it() => UiThread.Run(async () =>
    {
        var list = await Shown();
        var late = Hold(new InstanceQuery(Root: true, Open: false));
        var expanding = list.ToggleDone(Token);

        await list.ToggleDone(Token);
        late.SetResult();
        await expanding;

        Assert.Equal((false, 0), (list.IsDoneExpanded, list.DoneRows.Count));
    }, Token);

    [Fact]
    public async Task Recipe_link_opens_the_approved_recipe()
    {
        var list = await Shown();
        var group = list.Groups[0];
        var asked = new List<string>();
        list.RecipeRequested += (_, e) => asked.Add(e.Subject);

        group.OpenRecipe();

        Expect.Sequence([group.Recipe], asked);   // a bare id: the approved version
        Assert.StartsWith(Labels.RecipeLink[..^2], group.RecipeLinkName, StringComparison.Ordinal);
        Assert.Contains(group.Name, group.RecipeLinkName, StringComparison.Ordinal);
        Assert.DoesNotContain("workflow.", group.RecipeLinkName, StringComparison.Ordinal);
        Assert.Equal("How it works ▸", Labels.RecipeLink);
    }
}
