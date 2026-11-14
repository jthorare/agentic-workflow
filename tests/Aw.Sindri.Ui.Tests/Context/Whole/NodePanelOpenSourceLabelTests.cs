using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The panel's link button names the place the link goes to, and its accessible name holds the visible words (#3714).</summary>
[Lane(TestLane.Unit)]
public sealed class NodePanelOpenSourceLabelTests
{
    private const string Node = "feature:tabs.tab-manager";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<NodePanelViewModel> Open(string source)
    {
#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
        var served = $$"""
            {
              "mapVersion": "v1",
              "node": { "key": "{{Node}}", "kind": "feature", "label": "Tab manager", "provenance": "human", "confirmed": true, "trust": "trusted", "confidence": 1, "source": {{source}} },
              "aliases": [],
              "out": [],
              "in": []
            }
            """.ReplaceLineEndings("\n");
#pragma warning restore MA0136
        var api = new FakeSindriApi { NodeAnswer = new RouteAnswer(HttpStatusCode.OK, served) };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, Token)).Value);
        var page = new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, _ => { }, new FakeLauncher()));
        await page.Opened(Token);
        var rect = page.Whole.Geometry!.Nodes.First(n => n.Key == Node).Rect;
        page.Whole.PointerClicked(rect.CenterX, rect.CenterY);
        await page.Whole.Panel.Reading;
        return page.Whole.Panel;
    }

    [Theory]
    [InlineData("""{ "kind": "file", "ref": "a.ts", "url": "https://github.com/x/y/blob/main/a.ts" }""", "Open on GitHub ↗", "Open on GitHub")]
    [InlineData("""{ "kind": "ticket", "ref": "LIN-12", "url": "https://linear.app/x/issue/LIN-12" }""", "Open in Linear ↗", "Open in Linear")]
    [InlineData("""{ "kind": "commit", "ref": "abc", "url": "https://example.test/c/abc" }""", "Open source ↗", "Open source")]
    public async Task The_link_label_follows_the_place_and_the_name_holds_the_visible_words(string source, string label, string words)
    {
        var panel = await Open(source);

        Assert.Equal(label, panel.OpenSourceText);
        Assert.Contains(words, panel.OpenSourceName, StringComparison.Ordinal);
    }
}
