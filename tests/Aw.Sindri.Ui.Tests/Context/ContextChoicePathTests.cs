using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>On a shared name (SND-CONTEXT-037) the "Did you mean" choices that share a title also read their key's path, so the rows differ; a unique title reads as before (#3124).</summary>
[Lane(TestLane.Unit)]
public sealed class ContextChoicePathTests
{
    private const string Split = "feature:tabs.pane-split";
    private const string Pane = "thing:pane-split";
    private const string Grid = "concept:layout.grid";

    private static readonly RouteAnswer _shared = new(HttpStatusCode.Conflict,
        $$"""{"type":"about:blank","title":"SND-CONTEXT-037","status":409,"detail":"'layout:panes' names more than one node","traceId":"<trace>","candidates":["{{Split}}","{{Pane}}","{{Grid}}"],"choices":[{"key":"{{Split}}","label":"layout:panes","kind":"feature"},{"key":"{{Pane}}","label":"layout:panes","kind":"thing"},{"key":"{{Grid}}","label":"Grid layout","kind":"concept"}]}""");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<ContextPageViewModel> Refused()
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var page = new ContextPageViewModel(new ContextPorts(new FakeSindriApi { LensesAnswer = _ => _shared }, () => new Uri("http://127.0.0.1:1/"), clipboard, clock,
            new CatalogViewModel(clipboard, clock), _ => { }));
        page.AnchorText = "layout:panes";
        await page.Submit(Token);
        return page;
    }

    [Fact]
    public async Task Choices_that_share_a_title_each_read_their_keys_path()
    {
        var page = await Refused();

        // why: both rows read "layout:panes" and told apart only by the kind tag ("thing" means nothing to a person)
        Assert.Equal(["layout:panes, feature, tabs.pane-split", "layout:panes, thing, pane-split", "Grid layout, concept"], page.ChoiceRows.Select(c => c.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Only_a_shared_title_gets_a_path_and_every_key_stays()
    {
        var page = await Refused();

        Assert.Equal(["tabs.pane-split", "pane-split", string.Empty], page.ChoiceRows.Select(c => c.Path), StringComparer.Ordinal);
        Assert.Equal([" · tabs.pane-split", " · pane-split", string.Empty], page.ChoiceRows.Select(c => c.PathText), StringComparer.Ordinal);
        Assert.Equal(new ContextChoice(Grid, "Grid layout", "concept"), page.ChoiceRows[2]);
        Assert.Equal([Split, Pane, Grid], page.ChoiceRows.Select(c => c.Key), StringComparer.Ordinal);
    }

    [Fact]
    public void A_key_with_no_kind_reads_whole_as_its_path() =>
        Assert.Equal(["plain", "other:plain"], ContextChoice.WithPaths([new ContextChoice("plain", "Same", string.Empty), new ContextChoice("x:other:plain", "Same", string.Empty)]).Select(c => c.Path), StringComparer.Ordinal);
}
