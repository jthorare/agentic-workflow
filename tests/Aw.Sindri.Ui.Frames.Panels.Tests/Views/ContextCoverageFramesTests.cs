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
/// The coverage sentence under a question's answer (#3260, #3680, #3900) on the page, in the three states qa shoots: a typical answer, nothing
/// searched, and function names only partly searched because the code index is stale. Each sentence fits on one line at 1280 and 1000 px.
/// Saved under frames/ as f3-context-coverage-*.png when AW_UI_FRAMES=1.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ContextCoverageFramesTests : FrameHost
{
    private const string Question = "Tab groups";

    private static string Anchor => (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

    // The answer a question with nothing around it really has: no record or decision, no own, neighbouring or affected node, no file, so every band is empty
    // (the server derives the empty flags from these counts, LensSearchedBuilder) and the coverage counts are zero; the code index is not built. Flipping only the
    // flags on the full golden drew "Nothing searched" over a full map (design's hold on #4062).
    private static void NothingSearched(JsonNode node)
    {
        var bands = node["bands"]!;
        Empty(bands["intent"]!, "records", "decisions");
        Empty(bands["product"]!, "own", "neighbors", "affects", "edges");
        Empty(bands["code"]!, "files");
        bands["code"]!["counts"]!["featuresWithNoCode"] = 0;
        var coverage = node["coverage"]!;
        coverage["scope"] = 0;
        coverage["gaps"] = new JsonArray();
        Zero(coverage["intent"]!, "covered", "of");
        Zero(coverage["product"]!, "confirmed", "of");
        Zero(coverage["code"]!, "implemented", "of", "suggested");
        foreach (var band in (string[])["intent", "product", "code"])
        {
            node["searched"]!["bands"]![band]!["empty"] = true;
        }
    }

    // Each list emptied, and its count beside it zeroed.
    private static void Empty(JsonNode band, params string[] lists)
    {
        foreach (var list in lists)
        {
            band[list] = new JsonArray();
            band["counts"]![list] = 0;
        }
    }

    private static void Zero(JsonNode counts, params string[] names)
    {
        foreach (var name in names)
        {
            counts[name] = 0;
        }
    }

    // The code index is at another commit than the map, so the function names are only partly searched. A stale answer has names, so it carries no
    // "index not built" reason or code (the golden's unavailable answer did, which left the stale frame's Code band contradicting its sentence).
    private static void NamesStale(JsonNode node)
    {
        var symbols = node["bands"]!["code"]!["symbols"]!.AsObject();
        symbols["state"] = "stale";
        _ = symbols.Remove("reason");
        _ = symbols.Remove("code");
    }

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
        // An answer with nodes draws them; an empty one draws none and says so on the stage (ContextPageViewModel.ShowAnswerFiles).
        await Until(window, () => shell.Context.HasCoverage && (shell.Context.HasStageMessage || window.GetVisualDescendants().OfType<ContextMapWorld>().FirstOrDefault() is { NodeViews.Count: > 0 }), "the answer and its sentence never drew");
        Settle(window);
        return (window, shell);
    }

    private Task Shows(Action<JsonNode> state, string sentence, double width, string stem, string stage = "") => OnUi(async () =>
    {
        var (window, shell) = await Ask(state, width);

        Assert.Equal(stage, shell.Context.StageMessage);   // empty unless the answer draws no node

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
        Shows(_ => { }, "Searched Product. Why and Code are only partly searched. Function names aren't indexed.", width, "typical");

    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task An_answer_with_no_records_draws_nothing_searched_on_one_line(double width) =>
        Shows(NothingSearched, "Nothing searched. No Why, Product or Code records. Function names aren't indexed.", width, "none", "The map draws nothing for this question.");

    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task A_stale_code_index_draws_function_names_as_partly_searched_on_one_line(double width) =>
        Shows(NamesStale, "Searched Product. Why, Code and function names are only partly searched.", width, "stale");
}
