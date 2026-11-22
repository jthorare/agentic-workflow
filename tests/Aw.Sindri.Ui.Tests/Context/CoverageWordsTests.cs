using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The coverage words (#3260, #3680): one plain sentence that names what was searched and why the rest was not, read from the map-tabs golden.</summary>
[Lane(TestLane.Unit)]
public sealed class CoverageWordsTests
{
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

        Assert.Equal("Searched Code. No Why or Product records. Function names aren't indexed.", words);
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

        Assert.StartsWith("Searched function names. No Why, Product or Code records.", words, StringComparison.Ordinal);
    }

    [Fact]
    public void An_area_searched_in_part_is_named_as_partly_searched()
    {
        var words = CoverageWords.Sentence(View(node =>
        {
            EverythingSearched(node);
            node["coverage"]!["intent"]!["covered"] = 0;
        }), exact: false);

        Assert.Equal("Searched Product, Code and function names. Why is only partly searched.", words);
    }

    [Fact]
    public void Function_names_alone_partly_searched_take_a_plural_verb()
    {
        var words = CoverageWords.Sentence(View(node =>
        {
            EverythingSearched(node);
            node["bands"]!["code"]!["symbols"]!["state"] = "stale";
        }), exact: false);

        Assert.Equal("Searched Why, Product and Code. Function names are only partly searched.", words);
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

        Assert.Equal("No Why, Product or Code records. Function names are only partly searched.", words);
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

        Assert.Equal("Nothing searched. No Why, Product or Code records. Function names aren't indexed.", words);
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
    public void Every_area_searched_says_nothing() =>
        Assert.Equal(string.Empty, CoverageWords.Sentence(View(EverythingSearched), exact: false));

    [Fact]
    public void An_exact_hit_says_nothing_even_when_areas_were_skipped() =>
        Assert.Equal(string.Empty, CoverageWords.Sentence(View(OneSearchedThreeNot), exact: true));
}
