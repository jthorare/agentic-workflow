using System.Net;
using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>The concept layer's pin side (concept pins spec §3.6-§3.8, phase Q7b): marks, order, the waiting card, the head line, the buttons and the status line.</summary>
[Lane(TestLane.Unit)]
public sealed class ConceptLayerPinTests
{
    private static readonly MapRebuildView _idle = new(MapRebuildPhases.Idle, Progress: null, RetryAt: null, Stale: false, Failed: null);

    private static PinView Pin(string id, string name, string state = PinStates.Approved, string? change = null, params string[] features) =>
        new(id, name, "def of " + name, ["word"], features, FolderRules: 0, "profile", state, change);

    private static ConceptLayerViewModel Layer(IReadOnlyList<ContextConcept> concepts, List<PinSheetOpen>? opened = null, PinsView? pins = null)
    {
        var layer = new ConceptLayerViewModel(_ => { });
        layer.WirePins(opened is null ? _ => { } : opened.Add, () => "api", TimeZoneInfo.Utc);
        layer.Show(AConcepts.Ok(concepts));
        if (pins is not null)
        {
            layer.ShowPins(pins);
        }

        return layer;
    }

    private static PinsView Pins(MapRebuildView? rebuild = null, params PinView[] pins) => new(1, "api", "file-h", "v1", pins, rebuild ?? _idle);

    [Fact]
    public void Pinned_cards_come_first_in_profile_order_with_the_mark_Set_by_a_person_and_derived_cards_read_Suggested()
    {
        var layer = Layer(
            [AConcepts.Concept("concept:auth", "Auth"), AConcepts.Concept("concept:billing", "Billing", source: "override"), AConcepts.Concept("concept:notify", "Notify", source: "override")],
            pins: Pins(rebuild: null, Pin("notify", "Notify"), Pin("billing", "Billing")));

        Assert.Equal(["Notify", "Billing", "Auth"], [.. layer.Tiles.Select(t => t.Name)]);
        Assert.Equal(["Set by a person", "Set by a person", "Suggested"], [.. layer.Tiles.Select(t => t.MarkText)]);
        Assert.Equal(["PinCard.notify", "PinCard.billing", string.Empty], [.. layer.Tiles.Select(t => t.AutomationId)]);
    }

    [Fact]
    public void A_derived_card_carries_Pin_this_and_a_pinned_card_Edit_pin()
    {
        var layer = Layer([AConcepts.Concept("concept:auth", "Auth"), AConcepts.Concept("concept:billing", "Billing", source: "override")], pins: Pins(rebuild: null, Pin("billing", "Billing")));

        Assert.Equal((false, true), (layer.Tiles[0].ShowPinThis, layer.Tiles[0].ShowEditPin));
        Assert.Equal((true, false), (layer.Tiles[1].ShowPinThis, layer.Tiles[1].ShowEditPin));
        Assert.True(layer.ShowNewPin);
    }

    [Fact]
    public void Each_button_hands_the_shell_what_it_was_pressed_on()
    {
        List<PinSheetOpen> opened = [];
        var billing = Pin("billing", "Billing");
        var layer = Layer([AConcepts.Concept("concept:auth", "Auth"), AConcepts.Concept("concept:billing", "Billing", source: "override")], opened, Pins(rebuild: null, billing));

        layer.PinThis(layer.Tiles[1]);
        layer.EditPin(layer.Tiles[0]);
        layer.NewPin();

        Assert.Equal("Auth", opened[0].Card?.Name);
        Assert.Equal(billing, opened[1].Pin);
        Assert.True(opened[2].IsNew);
        Assert.All(opened, o => Assert.Equal("api", o.Repo));
        Assert.All(opened, o => Assert.Equal([billing], o.Pins));
    }

    [Fact]
    public void A_pin_with_no_node_in_the_map_is_a_waiting_card_at_the_head_of_the_pinned_cards()
    {
        var layer = Layer(
            [AConcepts.Concept("concept:billing", "Billing", source: "override"), AConcepts.Concept("concept:auth", "Auth")],
            pins: Pins(rebuild: null, Pin("billing", "Billing"), Pin("refunds", "Refunds", PinStates.Waiting)));

        var waiting = layer.Tiles[0];
        Assert.Equal(["Refunds", "Billing", "Auth"], [.. layer.Tiles.Select(t => t.Name)]);
        Assert.Equal(("Set by a person", "PinCard.refunds", "def of Refunds"), (waiting.MarkText, waiting.AutomationId, waiting.Summary));
        Assert.Equal(["word"], waiting.Terms);
        Assert.Equal(["Pinned, not approved yet", "Other changes are waiting, so this needs your review."], waiting.Lines);
    }

    [Fact]
    public void A_waiting_card_hides_the_Pin_this_card_it_came_from()
    {
        var layer = Layer(
            [AConcepts.Concept("concept:payments", "Payments"), AConcepts.Concept("concept:auth", "Auth")],
            pins: Pins(rebuild: null, Pin("billing-payments", "payments", PinStates.Waiting)));

        Assert.Equal(["payments", "Auth"], [.. layer.Tiles.Select(t => t.Name)]);
        Assert.False(layer.Tiles[0].ShowPinThis);
        Assert.Equal(["PinCard.billing-payments", string.Empty], [.. layer.Tiles.Select(t => t.AutomationId)]);
    }

    [Fact]
    public void The_real_card_takes_the_waiting_cards_automation_id_when_the_map_lands()
    {
        var layer = Layer([AConcepts.Concept("concept:auth", "Auth")], pins: Pins(rebuild: null, Pin("refunds", "Refunds", PinStates.Approved, "created")));
        Assert.Equal("PinCard.refunds", layer.Tiles[0].AutomationId);

        layer.Show(AConcepts.Ok([AConcepts.Concept("concept:auth", "Auth"), AConcepts.Concept("concept:refunds", "Refunds", source: "override")]));
        layer.ShowPins(Pins(rebuild: null, Pin("refunds", "Refunds")));

        Assert.Equal("PinCard.refunds", layer.Tiles.Single(t => t.Name == "Refunds").AutomationId);
        Assert.False(layer.Tiles.Single(t => t.Name == "Refunds").HasFooter);
    }

    [Fact]
    public void A_concept_a_person_set_whose_pin_is_gone_reads_Unpinned_until_the_rebuild()
    {
        var stale = new MapRebuildView(MapRebuildPhases.Idle, Progress: null, RetryAt: null, Stale: true, Failed: null);
        var layer = Layer([AConcepts.Concept("concept:billing", "Billing", source: "override")], pins: Pins(stale));

        Assert.Equal(["Unpinned · the map rebuild starts within a minute"], layer.Tiles[0].Lines);
        Assert.False(layer.Tiles[0].ShowEditPin);
    }

    [Fact]
    public void The_head_line_says_how_much_the_pins_cover()
    {
        var layer = Layer(
            [AConcepts.Concept("concept:billing", "Billing", features: 120, records: 410, source: "override"), AConcepts.Concept("concept:auth", "Auth", features: 180, records: 1890)],
            pins: Pins(rebuild: null, Pin("billing", "Billing")));

        Assert.Equal("You've pinned 120 of 300 features and 410 of 2,300 records. The rest are in suggestions or in no concept.", layer.PinHeadText);
    }

    [Fact]
    public void With_no_pins_the_head_line_says_how_to_make_one()
    {
        var layer = Layer([AConcepts.Concept("concept:auth", "Auth")], pins: Pins());

        Assert.Equal("No pins yet. Press Pin this on a concept to make it yours.", layer.PinHeadText);
    }

    [Fact]
    public void A_server_without_the_pins_route_leaves_every_pin_button_and_mark_off()
    {
        var layer = Layer([AConcepts.Concept("concept:auth", "Auth"), AConcepts.Concept("concept:billing", "Billing", source: "override")], pins: Pins(rebuild: null, Pin("billing", "Billing")));

        layer.PinsNotServed();

        Assert.False(layer.ShowNewPin);
        Assert.False(layer.ShowAddSeveral);
        Assert.Equal(string.Empty, layer.PinHeadText);
        Assert.All(layer.Tiles, t => Assert.False(t.ShowPinThis || t.ShowEditPin || t.HasFooter));
        Assert.Equal([string.Empty, "Set by a person"], [.. layer.Tiles.Select(t => t.MarkText)]);
    }

    [Fact]
    public void The_status_line_follows_the_rebuild_and_says_when_the_map_is_ready()
    {
        var layer = Layer([AConcepts.Concept("concept:auth", "Auth")], pins: Pins());
        Assert.False(layer.HasRebuildText);

        layer.ShowPins(Pins(new MapRebuildView(MapRebuildPhases.Building, "resolving calls", RetryAt: null, Stale: true, Failed: null)));
        Assert.Equal("Rebuilding the map: resolving calls", layer.RebuildText);

        layer.ShowPins(Pins());
        Assert.Equal("The map is ready", layer.RebuildText);
    }

    [Fact]
    public void A_footer_button_asks_for_what_it_names()
    {
        var asked = new List<string>();
        var layer = Layer([AConcepts.Concept("concept:billing", "Billing", source: "override")], pins: null);
        layer.ReviewAndApprove = () => asked.Add("review");
        layer.TryAgain = () => asked.Add("again");
        layer.ShowPins(Pins(rebuild: null, Pin("billing", "Billing", PinStates.Waiting)));
        layer.RunAction(layer.Tiles[0]);
        layer.ShowPins(Pins(new MapRebuildView(MapRebuildPhases.Idle, Progress: null, RetryAt: null, Stale: true, Failed: "SND-CONTEXT-040"), Pin("billing", "Billing", PinStates.Approved, "updated")));
        layer.RunAction(layer.Tiles[0]);

        Assert.Equal(["review", "again"], asked);
        Assert.Equal("Try again", layer.Tiles[0].ActionText);
    }

    [Fact]
    public async Task The_page_reads_the_pins_beside_the_concepts_and_a_404_leaves_the_layer_as_it_was()
    {
        var concepts = AConcepts.Ok([AConcepts.Concept("concept:auth", "Auth")]);
        var served = new FakeSindriApi
        {
            ContextConceptsAnswer = _ => AConcepts.Served(concepts),
            ContextPinsAnswer = _ => new RouteAnswer(HttpStatusCode.OK, Encoding.UTF8.GetString(AwJson.Serialize(Pins(), SindriApiClient.Json))),
        };
        var older = new FakeSindriApi { ContextConceptsAnswer = _ => AConcepts.Served(concepts) };

        var with = await Shown(served);
        var without = await Shown(older);

        Assert.True(with.Concepts.ShowNewPin);
        Assert.True(with.Concepts.Tiles[0].ShowPinThis);
        Assert.False(without.Concepts.ShowNewPin);
        Assert.False(without.Concepts.Tiles[0].ShowPinThis);
        Assert.NotEmpty(served.ContextPinsAsked);
    }

    private static async Task<ContextPageViewModel> Shown(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, TestContext.Current.CancellationToken)).Value);
        var page = new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, _ => { }, OpenPin: _ => { }));
        await page.Opened(TestContext.Current.CancellationToken);
        return page;
    }
}
