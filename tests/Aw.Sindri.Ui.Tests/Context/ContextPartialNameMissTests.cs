using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>A partial name the map refuses with SND-CONTEXT-003 and "Did you mean" rows is not "nothing matches" (#4419).</summary>
[Lane(TestLane.Unit)]
public sealed class ContextPartialNameMissTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    private static RouteAnswer WithChoices()
    {
        string[] numbers = ["001", "002", "003", "004", "005"];
        var keys = numbers.Select(n => "concept:invoice.invoice-" + n).ToArray();
        var choices = string.Join(',', numbers.Select(n => "{\"key\":\"concept:invoice.invoice-" + n + "\",\"label\":\"Invoice " + n + "\",\"kind\":\"concept\"}"));
        var anchors = string.Join(',', keys.Select(k => "\"" + k + "\""));
        var body = "{\"type\":\"about:blank\",\"title\":\"SND-CONTEXT-003\",\"status\":404,\"detail\":\"no node is named 'Invoice'\",\"traceId\":\"<trace>\",\"suggestedAnchors\":[" + anchors + "],\"choices\":[" + choices + "]}";
        return new RouteAnswer(HttpStatusCode.NotFound, body);
    }

    [Fact]
    public async Task A_partial_name_with_choices_says_no_item_is_named_it()
    {
        var page = Page(new FakeSindriApi { LensesAnswer = _ => WithChoices() });
        page.AnchorText = "Invoice";

        await page.Submit(Token);

        Assert.True(page.HasChoices);
        Assert.DoesNotContain("Nothing matches", page.Problem, StringComparison.Ordinal);
        Assert.Contains("No item is named 'Invoice'", page.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_miss_with_no_choices_still_says_nothing_matches()
    {
        var page = Page(new FakeSindriApi { LensesAnswer = _ => RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-003", "no node is named 'Invoice'") });
        page.AnchorText = "Invoice";

        await page.Submit(Token);

        Assert.False(page.HasChoices);
        Assert.Contains("Nothing matches 'Invoice'", page.Problem, StringComparison.Ordinal);
    }
}
