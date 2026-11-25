using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>Searches on the area tiles (#3674): each search's matches are the ranked list, a second one without Esc included, and none of them opens a feature on the canvas.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextWholeTilesSearchTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Anchor => (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    // The by-feature overview of a repo past the tile line (200 nodes); a locate or an area read is answered too, so a fall into the canvas zoom shows in WholeMapAsked.
    private static FakeSindriApi Tiles() => new()
    {
        WholeMapAnswer = q => q switch
        {
            { Locate.Count: > 0 } => RouteAnswer.Api("context-whole", "locate"),
            { Area: not null } => ContextAnswers.WholeMap(q),
            _ => Overview(),
        },
    };

    private static RouteAnswer Overview()
    {
        var golden = GoldenFiles.Node("context-whole", "areas-over-budget");
        golden["counts"]!["nodes"] = 200;
        return new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());
    }

    [Fact]
    public async Task A_second_search_on_the_tiles_without_esc_lists_its_own_matches_and_opens_no_feature()
    {
        var api = Tiles();
        var page = Page(api);
        await page.Opened(Token);
        Assert.True(page.Whole.ShowAreaTiles);
        page.MapSearch = new Searches(["Billing.cs", "Invoice.cs"], ["Ledger.cs"]);
        page.AnchorText = Anchor;
        await page.Submit(Token);
        Assert.Equal(["Billing.cs", "Invoice.cs"], page.Whole.Items.Select(i => i.Label), StringComparer.Ordinal);

        await page.Submit(Token);

        Assert.Equal(["Ledger.cs"], page.Whole.Items.Select(i => i.Label), StringComparer.Ordinal);
        Assert.True(page.Whole.IsSearchList);
        Assert.DoesNotContain(api.WholeMapAsked, q => q.Area is not null || q.Locate.Count > 0);
    }

    // Each search answers the next list of labels, as code files of one feature.
    private sealed class Searches(params string[][] answers) : IMapSearch
    {
        private int _next;

        public Task<IReadOnlyList<MapSearchHit>> Search(string query, CancellationToken cancellationToken)
        {
            var labels = answers[Math.Min(_next++, answers.Length - 1)];
            return Task.FromResult<IReadOnlyList<MapSearchHit>>([.. labels.Select(l => new MapSearchHit($"code:src/{l}", "code", l, "feature:billing"))]);
        }
    }
}
