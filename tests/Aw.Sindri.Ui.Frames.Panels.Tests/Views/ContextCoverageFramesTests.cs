using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Context.Map;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Panels.Tests.Views;

/// <summary>
/// The coverage sentence under a question's answer (#3260, #3680, #3900, #4122) on the page, in the three states qa shoots: a typical answer, nothing
/// searched, and function names only partly indexed because the code index is stale. Each sentence fits on one line at 1280 and 1000 px.
/// Saved under frames/ as f3-context-coverage-*.png when AW_UI_FRAMES=1.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ContextCoverageFramesTests : FrameHost
{
    private const string Question = "Tab groups";

    private const string NextStep = "See Code index on the System page.";

    private static string Anchor => (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

    // No band has a record; the code index is not built.
    private static void NothingSearched(JsonNode node)
    {
        node["searched"]!["bands"]!["intent"]!["empty"] = true;
        node["searched"]!["bands"]!["product"]!["empty"] = true;
        node["searched"]!["bands"]!["code"]!["empty"] = true;
    }

    // The code index is at another commit than the map, so the function names are only partly indexed.
    private static void NamesStale(JsonNode node) => node["bands"]!["code"]!["symbols"]!["state"] = "stale";

    // The question goes through the query (not an exact hit), so the page shows the sentence for the edited answer.
    private async Task<(ShellWindow Window, ShellViewModel Shell)> Ask(Action<JsonNode> state, double width)
    {
        var golden = GoldenFiles.Node("context-lenses", "map-tabs");
        state(golden);
        Server.WholeMapAnswer = _ => new RouteAnswer(HttpStatusCode.InternalServerError, "{}");   // no whole map: the question draws its neighbourhood
        Server.QueryAnswer = _ => FakeSindriApi.QueryAnchorsBody([Anchor], []);
        Server.LensesAnswer = _ => new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Context);
        shell.Context.AnchorText = Question;
        await shell.Context.Submit(Token);
        await Until(window, () => shell.Context.HasCoverage && window.GetVisualDescendants().OfType<ContextMapWorld>().FirstOrDefault() is { NodeViews.Count: > 0 }, "the answer and its sentence never drew");
        Settle(window);
        return (window, shell);
    }

    private Task Shows(Action<JsonNode> state, string sentence, double width, string stem) => OnUi(async () =>
    {
        var (window, shell) = await Ask(state, width);

        var coverage = Named<TextBlock>(window, "ContextCoverage");
        Assert.Equal(sentence, shell.Context.Coverage);
        Assert.Equal(sentence, coverage.Text);
        Assert.True(coverage.IsEffectivelyVisible, "the sentence is hidden");
        _ = Assert.Single(coverage.TextLayout.TextLines);   // why: at 420 px wide "aren't indexed." wrapped alone onto a second line (#3900)
        Frame(window, string.Create(CultureInfo.InvariantCulture, $"context-coverage-{stem}-{width:0}"), "f3-");
    });

    // The golden as served: Product in full, Why 2 of 6 and Code 2 of 6, the code index not built.
    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task A_typical_answer_draws_its_sentence_on_one_line(double width) =>
        Shows(_ => { }, "Searched all of Product and part of Why and Code. Function names aren't indexed yet. " + NextStep, width, "typical");

    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task An_answer_with_no_records_draws_nothing_searched_on_one_line(double width) =>
        Shows(NothingSearched, "Nothing searched. No Why, Product or Code records. Function names aren't indexed yet. " + NextStep, width, "none");

    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task A_stale_code_index_draws_function_names_as_partly_indexed_on_one_line(double width) =>
        Shows(NamesStale, "Searched all of Product and part of Why and Code. Function names are only partly indexed. " + NextStep, width, "stale");

    // #4184: the longest sentence, about 150 characters: a reason clause and its next step. One line at 1280; at 1000 a second line is fine, as long as
    // every word is drawn (nothing trimmed, nothing cut off by the page around it).
    [Theory]
    [InlineData(1280, 1)]
    [InlineData(1000, 2)]
    public Task A_reason_with_its_next_step_draws_whole_in_at_most_its_lines(double width, int lines) => OnUi(async () =>
    {
        var sentence = "Searched all of Product and part of Why and Code. Function names aren't indexed: no code index is set up for this repo. " + NextStep;
        var (window, shell) = await Ask(node => node["bands"]!["code"]!["symbols"]!["reason"] = "not configured", width);
        Frame(window, string.Create(CultureInfo.InvariantCulture, $"context-coverage-reason-{width:0}"), "f3-");

        var coverage = Named<TextBlock>(window, "ContextCoverage");
        var parent = coverage.GetVisualParent();
        Assert.Equal(sentence, shell.Context.Coverage);
        Assert.Equal(sentence, coverage.Text);
        Assert.True(coverage.IsEffectivelyVisible, "the sentence is hidden");
        Assert.InRange(coverage.TextLayout.TextLines.Count, 1, lines);
        Assert.DoesNotContain(coverage.TextLayout.TextLines, static line => line.HasCollapsed);
        Assert.True(coverage.Bounds.Height >= coverage.TextLayout.Height - 0.5, string.Create(CultureInfo.InvariantCulture, $"the text is {coverage.TextLayout.Height} tall in {coverage.Bounds.Height}"));
        Assert.NotNull(parent);
        Assert.True(coverage.Bounds.Bottom <= parent.Bounds.Height + 0.5 && coverage.Bounds.Right <= parent.Bounds.Width + 0.5, "the page around the sentence cuts it off");
    });
}
