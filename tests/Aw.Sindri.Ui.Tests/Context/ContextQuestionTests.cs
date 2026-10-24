using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The Context box asks the question before it guesses anchors: a phrase goes to the free-text query, anchor-shaped text goes straight to the lenses.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextQuestionTests
{
    private const string Phrase = "manager schedule drag and drop shifts";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Tabs { get; } = (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    [Fact]
    public async Task A_phrase_runs_the_query_then_reads_the_lenses_for_the_returned_anchors()
    {
        var server = new FakeSindriApi { QueryAnswer = _ => FakeSindriApi.QueryAnchorsBody([Tabs], ["feature:other"]) };
        var page = Page(server);
        page.AnchorText = Phrase;

        await page.Submit(Token);

        Assert.Equal([$"query:{Phrase}", $"lenses:{Tabs}"], server.ContextLog);
        Assert.Equal(Labels.ContextFromQuestion(1), page.Head);
    }

    [Fact]
    public async Task Anchor_shaped_text_skips_the_query()
    {
        var server = new FakeSindriApi();
        var page = Page(server);
        page.AnchorText = Tabs;

        await page.Submit(Token);

        Assert.Equal([$"lenses:{Tabs}"], server.ContextLog);
        Assert.Equal(Labels.ContextHead(1), page.Head);
    }

    [Fact]
    public async Task No_anchors_from_the_query_fall_back_to_its_suggestions()
    {
        var server = new FakeSindriApi { QueryAnswer = _ => FakeSindriApi.QueryAnchorsBody([], [Tabs]) };
        var page = Page(server);
        page.AnchorText = Phrase;

        await page.Submit(Token);

        Assert.Equal([$"query:{Phrase}", $"lenses:{Tabs}"], server.ContextLog);
    }

    [Fact]
    public async Task No_anchors_and_no_suggestions_fall_back_to_the_word_split_and_its_problem()
    {
        var server = new FakeSindriApi();
        var page = Page(server);
        page.AnchorText = "alpha beta";

        await page.Submit(Token);

        Assert.Equal(["query:alpha beta", "lenses:alpha beta"], server.ContextLog);
        Assert.True(page.HasProblem);
    }

    [Fact]
    public async Task A_query_refusal_falls_back_to_the_word_split()
    {
        var server = new FakeSindriApi { QueryAnswer = _ => RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-CONTEXT-999", "The query failed.") };
        var page = Page(server);
        page.AnchorText = "alpha beta";

        await page.Submit(Token);

        Assert.Equal(["query:alpha beta", "lenses:alpha beta"], server.ContextLog);
    }

    [Fact]
    public async Task A_control_plane_that_is_down_shows_its_problem_and_reads_no_lenses()
    {
        var server = new FakeSindriApi();
        server.GoDown();
        var page = Page(server);
        page.AnchorText = Phrase;

        await page.Submit(Token);

        Assert.Equal([$"query:{Phrase}"], server.ContextLog);
        Assert.True(page.HasProblem);
    }

    [Fact]
    public async Task A_newer_submit_discards_an_older_querys_answer()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new FakeSindriApi { QueryGate = gate, QueryAnswer = _ => FakeSindriApi.QueryAnchorsBody(["feature:older"], []) };
        var page = Page(server);
        page.AnchorText = Phrase;
        var older = page.Submit(Token);
        page.AnchorText = Tabs;

        await page.Submit(Token);
        gate.SetResult();
        await older;

        Assert.Equal([$"query:{Phrase}", $"lenses:{Tabs}"], server.ContextLog);
        Assert.Equal(Labels.ContextHead(1), page.Head);
    }

    [Fact]
    public async Task A_question_that_resolves_to_fifty_anchors_sends_all_of_them()
    {
        var anchors = Enumerable.Range(0, 50).Select(i => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"a:{i}")).ToList();
        var sent = new List<int>();
        var server = new FakeSindriApi
        {
            QueryAnswer = _ => FakeSindriApi.QueryAnchorsBody(anchors, []),
            LensesAnswer = q => { sent.Add(q.Anchors.Count); return ContextAnswers.Lenses(q); },
        };
        var page = Page(server);
        page.AnchorText = Phrase;

        await page.Submit(Token);

        Assert.Equal([50], sent);
    }
}
