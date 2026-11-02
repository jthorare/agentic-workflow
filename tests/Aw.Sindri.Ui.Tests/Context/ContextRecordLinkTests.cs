using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>A lens record's citation in the inspector (#1702): "Open in Linear" when the answer gives a link, "no link" when it cites the record without one, nothing when it does not cite it.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextRecordLinkTests
{
    private const string Manager = "feature:tabs.tab-manager";
    private const string Ticket = "record:linear:issue/0e5a1b00-0000-4000-8000-000000000203";
    private const string LinearUrl = "https://linear.app/split-panes/issue/SPL-103/tab-manager-spec";
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The golden lenses answer, with the ticket's record cited as C1 (a permalink, or none) or left as it is.
    private static RouteAnswer Lenses(string? permalink, bool cited)
    {
        var body = GoldenFiles.Node("context-lenses", "map-tabs");
        if (cited)
        {
            var record = body["bands"]!["intent"]!["records"]!.AsArray().First(r => (string)r!["key"]! == Ticket)!;
            record["cites"] = new JsonArray("C1");
            record["state"] = "current";
            body["cites"] = new JsonObject
            {
                ["C1"] = new JsonObject
                {
                    ["key"] = "linear:issue/0e5a1b00-0000-4000-8000-000000000203",
                    ["refs"] = new JsonArray(),
                    ["permalink"] = permalink,
                    ["trust"] = "untrusted",
                    ["state"] = "current",
                    ["aliases"] = new JsonArray("SPL-103"),
                },
            };
        }

        return new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
    }

    private static async Task<(NodeCardViewModel Card, FakeLauncher Launcher)> Open(RouteAnswer lenses)
    {
        var api = new FakeSindriApi { LensesAnswer = _ => lenses };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var launcher = new FakeLauncher();
        var page = new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }, launcher)) { AnchorText = Manager };
        await page.Submit(Token);
        await page.Select(Ticket, Token);
        return (page.Node, launcher);
    }

    [Fact]
    public async Task A_record_with_a_permalink_offers_a_button_that_opens_it_through_the_launcher()
    {
        var (card, launcher) = await Open(Lenses(LinearUrl, cited: true));

        Assert.True(card.HasLink);
        Assert.False(card.NoLink);
        Assert.Equal("Open in Linear", card.OpenText);
        Assert.Equal("Open SPL-103 in Linear", card.OpenName);
        Assert.Equal(new Uri(LinearUrl), card.Link);

        await card.OpenRecord(Token);

        Assert.Equal([LinearUrl], launcher.Opened);
    }

    [Fact]
    public async Task A_cited_record_with_no_permalink_says_no_link_and_has_no_button()
    {
        var (card, launcher) = await Open(Lenses(permalink: null, cited: true));

        Assert.False(card.HasLink);
        Assert.True(card.NoLink);
        Assert.Equal("no link", card.NoLinkText);

        await card.OpenRecord(Token);

        Assert.Empty(launcher.Opened);
    }

    [Fact]
    public async Task A_record_with_no_citation_shows_neither()
    {
        var (card, _) = await Open(Lenses(permalink: null, cited: false));

        Assert.False(card.HasLink);
        Assert.False(card.NoLink);
        Assert.Equal(string.Empty, card.OpenText);
    }
}
