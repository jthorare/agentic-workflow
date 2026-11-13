using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>"Did you mean" shows the titles the map's cards use, with the kind as a tag, and a pick still reads the key (#2809).</summary>
[Lane(TestLane.Unit)]
public sealed class ContextChoiceTitlesTests
{
    private const string Restore = "action:tab-manager.restore-layout";
    private const string Groups = "concept:tabs.tab-groups";

    private static readonly RouteAnswer _unknown = new(HttpStatusCode.NotFound,
        $$"""{"type":"about:blank","title":"SND-CONTEXT-003","status":404,"detail":"no node is named 'how are tabs restored' (fix: try {{Restore}}, {{Groups}})","traceId":"<trace>","suggestedAnchors":["{{Restore}}","{{Groups}}"],"choices":[{"key":"{{Restore}}","label":"Tab layout and restore","kind":"action"},{"key":"{{Groups}}","label":"Tab groups","kind":"concept"}]}""");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    [Fact]
    public async Task An_unknown_anchor_offers_titles_with_their_kind_and_a_pick_reads_the_key()
    {
        var server = new FakeSindriApi { LensesAnswer = _ => _unknown };
        var page = Page(server);
        page.AnchorText = "feature:tabs.nope";

        await page.Submit(Token);

        Assert.Equal([new ContextChoice(Restore, "Tab layout and restore", "action"), new ContextChoice(Groups, "Tab groups", "concept")], page.ChoiceRows);

        await page.Pick(page.ChoiceRows[1].Key, Token);
        Assert.Equal(Groups, page.AnchorText);
    }

    [Fact]
    public async Task A_question_with_no_anchors_offers_its_suggestions_by_title()
    {
        var server = new FakeSindriApi
        {
            QueryAnswer = _ => new RouteAnswer(HttpStatusCode.OK,
                $$"""{"anchors":[],"suggestedAnchors":["{{Groups}}"],"suggestions":[{"key":"{{Groups}}","label":"Tab groups","kind":"concept"}]}"""),
            LensesAnswer = _ => RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-003", "No such anchor."),
        };
        var page = Page(server);
        page.AnchorText = "how are tabs restored";

        await page.Submit(Token);

        Assert.Equal([new ContextChoice(Groups, "Tab groups", "concept")], page.ChoiceRows);
    }
}
