using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Context.Layout;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>
/// The coverage words (#3260, #3680, #4122): one plain sentence that names what was searched and why the rest was not, read from the map-tabs golden.
/// The function names have one word, "indexed", in the sentence and on the code band, and a sentence about missing names ends with where to look.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class CoverageWordsTests
{
    private const string NextStep = "See Code index on the Health page.";

    private static LensesView View(Action<JsonNode> edit)
    {
        var node = GoldenFiles.Node("context-lenses", "map-tabs");
        edit(node);
        var read = AwJson.Deserialize<LensesView>(Encoding.UTF8.GetBytes(node.ToJsonString()), SindriApiClient.Json);
        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : string.Empty);
        return read.Value;
    }

    // Code searched in full; intent and product have no records; the function names are not indexed.
    internal static void OneSearchedThreeNot(JsonNode node)
    {
        node["searched"]!["bands"]!["intent"]!["empty"] = true;
        node["searched"]!["bands"]!["product"]!["empty"] = true;
        node["coverage"]!["code"]!["implemented"] = node["coverage"]!["code"]!["of"]!.GetValue<int>();
        node["bands"]!["code"]!["symbols"]!["state"] = "unavailable";
    }

    private static void EverythingSearched(JsonNode node)
    {
        node["coverage"]!["intent"]!["covered"] = node["coverage"]!["intent"]!["of"]!.GetValue<int>();
        node["coverage"]!["code"]!["implemented"] = node["coverage"]!["code"]!["of"]!.GetValue<int>();
        node["bands"]!["code"]!["symbols"]!["state"] = "ok";
    }

    [Fact]
    public void One_area_searched_and_three_that_were_not_read_as_one_sentence_with_the_reasons()
    {
        var words = CoverageWords.Sentence(View(OneSearchedThreeNot), exact: false);

        Assert.Equal("Searched Code. No Why or Product records. Function names aren't indexed yet. " + NextStep, words);
    }

    [Fact]
    public void Three_areas_without_records_are_joined_with_commas_and_a_last_or()
    {
        var words = CoverageWords.Sentence(View(node =>
        {
            EverythingSearched(node);
            node["searched"]!["bands"]!["intent"]!["empty"] = true;
            node["searched"]!["bands"]!["product"]!["empty"] = true;
            node["searched"]!["bands"]!["code"]!["empty"] = true;
        }), exact: false);

        Assert.Equal("No Why, Product or Code records.", words);   // the function names were searched, so the sentence does not open with "Nothing searched"
    }

    [Fact]
    public void An_area_searched_in_part_joins_the_areas_searched_in_full_in_one_sentence()
    {
        var words = CoverageWords.Sentence(View(node =>
        {
            EverythingSearched(node);
            node["coverage"]!["intent"]!["covered"] = 0;
        }), exact: false);

        Assert.Equal("Searched all of Product and Code, and part of Why.", words);
    }

    [Fact]
    public void Function_names_from_a_stale_index_are_partly_indexed_and_name_a_next_step()
    {
        var words = CoverageWords.Sentence(View(node =>
        {
            EverythingSearched(node);
            node["bands"]!["code"]!["symbols"]!["state"] = "stale";
        }), exact: false);

        Assert.Equal("Searched Why, Product and Code. Function names are only partly indexed. " + NextStep, words);
    }

    [Fact]
    public void Nothing_searched_in_full_but_something_in_part_does_not_open_with_nothing_searched()
    {
        var words = CoverageWords.Sentence(View(node =>
        {
            EverythingSearched(node);
            node["searched"]!["bands"]!["intent"]!["empty"] = true;
            node["searched"]!["bands"]!["product"]!["empty"] = true;
            node["searched"]!["bands"]!["code"]!["empty"] = true;
            node["bands"]!["code"]!["symbols"]!["state"] = "stale";
        }), exact: false);

        Assert.Equal("No Why, Product or Code records. Function names are only partly indexed. " + NextStep, words);
    }

    [Fact]
    public void Nothing_searched_at_all_opens_with_nothing_searched()
    {
        var words = CoverageWords.Sentence(View(node =>
        {
            node["searched"]!["bands"]!["intent"]!["empty"] = true;
            node["searched"]!["bands"]!["product"]!["empty"] = true;
            node["searched"]!["bands"]!["code"]!["empty"] = true;
            node["bands"]!["code"]!["symbols"]!["state"] = "unavailable";
        }), exact: false);

        Assert.Equal("Nothing searched. No Why, Product or Code records. Function names aren't indexed yet. " + NextStep, words);
    }

    [Fact]
    public void The_line_names_the_bands_with_the_map_band_labels_only()
    {
        var words = CoverageWords.Sentence(View(node =>
        {
            node["searched"]!["bands"]!["intent"]!["empty"] = true;
            node["searched"]!["bands"]!["product"]!["empty"] = true;
            node["searched"]!["bands"]!["code"]!["empty"] = true;
            node["bands"]!["code"]!["symbols"]!["state"] = "unavailable";
        }), exact: false);
        var bands = string.Join(", ", NodeBands.Title(WholeBand.Intent), NodeBands.Title(WholeBand.Product)) + " or " + NodeBands.Title(WholeBand.Code);

        Assert.Contains("No " + bands + " records.", words, StringComparison.Ordinal);
        Assert.DoesNotContain("intent", words, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Tickets and decisions", words, StringComparison.Ordinal);
    }

    [Fact]
    public void Areas_searched_only_in_part_open_with_part_of()
    {
        var words = CoverageWords.Sentence(View(node =>
        {
            EverythingSearched(node);
            node["coverage"]!["intent"]!["covered"] = 0;
            node["coverage"]!["code"]!["implemented"] = 0;
            node["searched"]!["bands"]!["product"]!["empty"] = true;
        }), exact: false);

        Assert.Equal("Searched part of Why and Code. No Product records.", words);
    }

    [Fact]
    public void The_sentence_and_the_code_band_say_indexed_for_function_names_that_are_missing()
    {
        var view = View(OneSearchedThreeNot);
        var sentence = CoverageWords.Sentence(view, exact: false);
        var band = ContextMapLayout.Build(view).Bands[2].Meta;

        Assert.Contains("Function names aren't indexed yet.", sentence, StringComparison.Ordinal);
        Assert.EndsWith("function names not indexed yet", band, StringComparison.Ordinal);
        Assert.DoesNotContain("available", sentence + band, StringComparison.Ordinal);
    }

    [Fact]
    public void A_stale_index_puts_the_same_partly_indexed_note_on_the_code_band()
    {
        var view = View(node =>
        {
            EverythingSearched(node);
            node["bands"]!["code"]!["symbols"]!["state"] = "stale";
        });

        Assert.Contains("Function names are only partly indexed.", CoverageWords.Sentence(view, exact: false), StringComparison.Ordinal);
        Assert.EndsWith("function names partly indexed", ContextMapLayout.Build(view).Bands[2].Meta, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_area_searched_says_nothing() =>
        Assert.Equal(string.Empty, CoverageWords.Sentence(View(EverythingSearched), exact: false));

    [Fact]
    public void An_exact_hit_says_nothing_even_when_areas_were_skipped() =>
        Assert.Equal(string.Empty, CoverageWords.Sentence(View(OneSearchedThreeNot), exact: true));
}
