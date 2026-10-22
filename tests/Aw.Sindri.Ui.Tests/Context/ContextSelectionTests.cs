using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>Selection and the inspector (F3 spec 1 §8.3): a node picked by click or J and K opens the inspector, which reads the node by key, says why it is in the map, shows a conflict, and sends Accept or Reject.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextSelectionTests
{
    private const string Manager = "feature:tabs.tab-manager";
    private const string ManagerFile = "code:src/tabs/TabManager.ts";
    private const string SuggestedFile = "code:src/tabs/TabGroups.ts";
    private const string Ticket = "record:linear:issue/0e5a1b00-0000-4000-8000-000000000203";
    private const string NewerDecision = "decision:ef895edc359f6f847ce34baeacb3670a81a824b521e4cd14371b5dc8894ea1c2";
    private const string OlderDecision = "decision:d036352234b17baf9a7b2e7cffef599bdc9c8209e92c389d2bcbb919c74c162b";
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<ContextPageViewModel> Open(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var page = new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { })) { AnchorText = Manager };
        await page.Submit(Token);
        Assert.NotNull(page.Map);
        return page;
    }

    [Fact]
    public async Task Choosing_a_node_opens_the_inspector_and_reads_that_node_by_key()
    {
        var api = new FakeSindriApi();
        var page = await Open(api);
        Assert.False(page.InspectorOpen);

        await page.Select(ManagerFile, Token);

        Assert.True(page.InspectorOpen);
        Assert.Equal(ManagerFile, page.Map!.SelectedKey);
        Assert.True(page.Map.Node(ManagerFile)!.IsSelected);
        Assert.Contains("node:" + ManagerFile, api.ContextLog, StringComparer.Ordinal);
        Assert.Equal("TabManager.ts", page.Node.Title);
    }

    [Fact]
    public async Task A_key_the_map_does_not_hold_changes_nothing()
    {
        var api = new FakeSindriApi();
        var page = await Open(api);

        await page.Select("feature:nothing", Token);

        Assert.False(page.InspectorOpen);
        Assert.DoesNotContain(api.ContextLog, e => e.StartsWith("node:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task J_and_K_walk_the_nodes_in_reading_order_and_stop_at_the_ends()
    {
        var page = await Open(new FakeSindriApi());
        var nodes = page.Map!.Nodes;

        await page.SelectNext(Token);
        Assert.Equal(nodes[0].Key, page.Map.SelectedKey);
        await page.SelectNext(Token);
        Assert.Equal(nodes[1].Key, page.Map.SelectedKey);
        await page.SelectPrevious(Token);
        await page.SelectPrevious(Token);
        Assert.Equal(nodes[0].Key, page.Map.SelectedKey);

        for (var i = 0; i < nodes.Count + 2; i++)
        {
            await page.SelectNext(Token);
        }

        Assert.Equal(nodes[^1].Key, page.Map.SelectedKey);
    }

    [Fact]
    public async Task K_from_nothing_picks_the_last_node()
    {
        var page = await Open(new FakeSindriApi());

        await page.SelectPrevious(Token);

        Assert.Equal(page.Map!.Nodes[^1].Key, page.Map.SelectedKey);
    }

    [Fact]
    public async Task Escape_closes_the_inspector_and_clears_the_selection()
    {
        var page = await Open(new FakeSindriApi());
        await page.Select(ManagerFile, Token);

        page.CloseInspector();

        Assert.False(page.InspectorOpen);
        Assert.Null(page.Map!.SelectedKey);
        Assert.All(page.Map.Nodes, n => Assert.False(n.IsSelected));
        Assert.Equal(string.Empty, page.Node.Title);
    }

    [Fact]
    public async Task The_inspector_says_how_the_node_is_reached_from_the_starting_point()
    {
        var page = await Open(new FakeSindriApi());

        await page.Select(ManagerFile, Token);
        Assert.Equal("Starts at Tab manager, then TabManager.ts (implemented in).", page.Node.Why);

        await page.Select(Manager, Token);
        Assert.Equal(Labels.ContextMapPathAnchor, page.Node.Why);
    }

    [Fact]
    public async Task The_connected_list_is_the_nodes_own_edges_in_and_out()
    {
        var served = GoldenFiles.Node("context-node", "tab-manager");
        var page = await Open(new FakeSindriApi());

        await page.Select(Manager, Token);

        Assert.Equal(served["in"]!.AsArray().Count + served["out"]!.AsArray().Count, page.Node.Connected.Count);
        Assert.Contains(page.Node.Connected, r => r.Label == "Tab groups");
    }

    [Fact]
    public async Task A_decision_in_a_conflict_names_the_other_decision_and_what_both_apply_to()
    {
        var page = await Open(new FakeSindriApi());

        await page.Select(NewerDecision, Token);

        Assert.True(page.Node.HasConflict);
        var other = page.Map!.Node(OlderDecision)!.Label;
        Assert.Equal(Labels.ContextInspConflictBody(other, page.Map.Node("feature:tabs.tab-groups")!.Label), page.Node.ConflictBody);
    }

    [Fact]
    public async Task A_node_in_no_conflict_has_no_conflict_section()
    {
        var page = await Open(new FakeSindriApi());

        await page.Select(ManagerFile, Token);

        Assert.False(page.Node.HasConflict);
    }

    [Fact]
    public async Task Accept_on_a_suggested_file_sends_the_desk_verb_with_its_link()
    {
        var api = new FakeSindriApi();
        var page = await Open(api);
        await page.Select(SuggestedFile, Token);
        Assert.True(page.Node.CanReview);

        await page.Node.Accept(Token);

        var call = Assert.Single(api.ReviewCalls);
        Assert.Equal((DeskVerb.Accept, "edge:402b8bed42f6611c"), (call.Verb, call.Link));
        Assert.Equal(Labels.ContextInspSaved, page.Node.Status);
    }

    [Fact]
    public async Task Reject_sends_the_reject_verb_and_a_refusal_is_shown_as_served()
    {
        var api = new FakeSindriApi { ReviewDecided = RouteAnswer.Problem("context-map-review", "stale-047") };
        var page = await Open(api);
        await page.Select(SuggestedFile, Token);

        await page.Node.Reject(Token);

        Assert.Equal(DeskVerb.Reject, Assert.Single(api.ReviewCalls).Verb);
        Assert.NotEqual(Labels.ContextInspSaved, page.Node.Status, StringComparer.Ordinal);
        Assert.NotEmpty(page.Node.Problem);
    }

    [Fact]
    public async Task A_confirmed_node_offers_no_accept_and_sends_nothing()
    {
        var api = new FakeSindriApi();
        var page = await Open(api);
        await page.Select(ManagerFile, Token);

        await page.Node.Accept(Token);

        Assert.False(page.Node.CanReview);
        Assert.Empty(api.ReviewCalls);
    }

    [Fact]
    public async Task A_tickets_source_row_says_when_it_was_last_read_and_how_sure_the_map_is()
    {
        var synced = A.Noon.AddHours(-2).ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        var body = new JsonObject
        {
            ["sources"] = new JsonArray(new JsonObject { ["source"] = "linear", ["kind"] = "linear", ["repo"] = null, ["state"] = "ok", ["detail"] = string.Empty, ["code"] = null, ["lastSync"] = synced, ["records"] = 3 }),
            ["sessionsEnabled"] = false,
            ["asOf"] = A.Noon.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
        };
        var api = new FakeSindriApi { SourcesAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString()) };
        var page = await Open(api);

        await page.Select(Ticket, Token);

        var row = Assert.Single(page.Sources.Rows);
        Assert.Equal(("linear", Labels.ContextInspSynced(TimeSpan.FromHours(2)), Labels.ContextInspSure(confirmed: true, "human", 1)), (row.Source, row.When, row.Sure));
    }

    [Fact]
    public async Task A_node_from_no_synced_source_is_named_by_who_found_it()
    {
        var page = await Open(new FakeSindriApi());

        await page.Select(ManagerFile, Token);

        var row = Assert.Single(page.Sources.Rows);
        Assert.Equal(Labels.ContextInspOrigin(page.Map!.Node(ManagerFile)!.Layout.Provenance), row.Source);
    }

    [Fact]
    public async Task A_newer_read_replaces_the_selection()
    {
        var page = await Open(new FakeSindriApi());
        await page.Select(ManagerFile, Token);

        page.AnchorText = Manager;
        await page.Submit(Token);

        Assert.False(page.InspectorOpen);
        Assert.Null(page.Map!.SelectedKey);
    }
}
