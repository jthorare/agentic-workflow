using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#3084, the ticket's own example: searching "judge" on the overview filters to the "Judge decisions" card.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextWholeCardTitleSearchTests
{
    private const string JudgeCard = "feature:tabs.tab-groups";   // why: the areas golden's card key; only its label is swapped to the ticket's title

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<ContextPageViewModel> OverviewWithJudgeCard()
    {
        var body = GoldenFiles.Api("context-whole", "areas-over-budget").Replace("\"Tab groups\"", "\"Judge decisions\"", StringComparison.Ordinal);
        Assert.Contains("\"Judge decisions\"", body, StringComparison.Ordinal);
        var api = new FakeSindriApi
        {
            WholeMapAnswer = q => q.Area is null && q.Locate.Count == 0 ? new RouteAnswer(HttpStatusCode.OK, body) : ContextAnswers.WholeMap(q),
            LensesAnswer = _ => RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-003", "no node is named that"),
        };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var page = new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
        await page.Opened(Token);
        Assert.Contains(page.Whole.Areas, a => string.Equals(a.Label, "Judge decisions", StringComparison.Ordinal));
        return page;
    }

    [Fact]
    public async Task Searching_judge_filters_to_the_Judge_decisions_card()
    {
        var page = await OverviewWithJudgeCard();
        page.AnchorText = "judge";

        await page.Submit(Token);

        Assert.Equal((true, false, false), (page.Whole.SearchActive, page.HasSearchNote, page.HasProblem));
        Assert.Equal([JudgeCard], page.Whole.Shown!.Tiles.Select(t => t.Key).Where(k => string.Equals(k, JudgeCard, StringComparison.Ordinal)));
        Assert.DoesNotContain(page.Whole.Shown!.Tiles, t => string.Equals(t.Key, "area:unassigned", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Searching_judge_in_capitals_filters_to_the_same_card()
    {
        var page = await OverviewWithJudgeCard();
        page.AnchorText = "JUDGE";

        await page.Submit(Token);

        Assert.Contains(page.Whole.Shown!.Tiles, t => string.Equals(t.Key, JudgeCard, StringComparison.Ordinal));
    }
}
