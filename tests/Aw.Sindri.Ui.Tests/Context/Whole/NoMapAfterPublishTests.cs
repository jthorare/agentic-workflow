using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#3046: a No map answer with no building facts, then a map: the next good read ends the No map state, so the body and the head agree on v1.</summary>
[Lane(TestLane.Unit)]
public sealed class NoMapAfterPublishTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_good_whole_map_read_after_a_no_map_answer_drops_the_no_map_state()
    {
        var api = new FakeSindriApi { WholeMapAnswer = _ => RouteAnswer.Problem("context-whole", "no-map-030") };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, Token)).Value);
        var page = new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, _ => { }));
        await page.Opened(Token);
        Assert.True(page.ShowEmpty);

        api.WholeMapAnswer = ContextAnswers.WholeMap;   // the map is published
        await page.ReadWhole(Token);

        Assert.Equal((false, false, "Context · the whole map · v1"), (page.ShowEmpty, page.NoMap, page.Head));
    }
}
