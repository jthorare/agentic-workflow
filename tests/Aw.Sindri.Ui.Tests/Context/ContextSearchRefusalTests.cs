using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>UX pass C-2: the box sends the whole question, a miss is said in plain words (never the CLI's fix text) and the map that was on the page stays.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextSearchRefusalTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Tabs { get; } = (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

    private static RouteAnswer Unknown(Ui.Api.Context.LensesQuery query) =>
        query.Anchors.Contains(Tabs, StringComparer.Ordinal) ? ContextAnswers.Lenses(query)
            : RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-003", "no node is named 'Tab' (fix: sindri context map status)");

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    [Fact]
    public async Task A_question_nothing_answers_is_read_whole_not_split_at_its_spaces()
    {
        var asked = new List<IReadOnlyList<string>>();
        var api = new FakeSindriApi { LensesAnswer = q => { asked.Add(q.Anchors); return Unknown(q); } };
        var page = Page(api);
        page.AnchorText = "Tab groups";

        await page.Submit(Token);

        Assert.Equal(["Tab groups"], Assert.Single(asked));
    }

    [Fact]
    public async Task A_miss_is_said_in_plain_words_without_the_cli_fix_text()
    {
        var api = new FakeSindriApi { LensesAnswer = Unknown };
        var page = Page(api);
        page.AnchorText = "Tab groups";

        await page.Submit(Token);

        Assert.True(page.HasProblem);
        Assert.DoesNotContain("fix:", page.Problem, StringComparison.Ordinal);
        Assert.DoesNotContain("sindri context", page.Problem, StringComparison.Ordinal);
        Assert.Contains("Tab groups", page.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_search_keeps_the_map_that_was_on_the_page()
    {
        var api = new FakeSindriApi { LensesAnswer = Unknown };
        var page = Page(api);
        page.AnchorText = Tabs;
        await page.Submit(Token);
        Assert.NotNull(page.Map);

        page.AnchorText = "nothing like this";
        await page.Submit(Token);

        Assert.NotNull(page.Map);
        Assert.True(page.HasProblem);
    }
}
