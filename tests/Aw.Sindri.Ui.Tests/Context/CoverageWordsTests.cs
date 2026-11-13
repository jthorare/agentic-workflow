using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The coverage words (#3260): one plain sentence that names what was searched and why the rest was not, read from the map-tabs golden.</summary>
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
    private static void OneSearchedThreeNot(JsonNode node)
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

        Assert.Equal("Searched code. No intent or product records. Function names not indexed.", words);
    }

    [Fact]
    public void An_area_searched_in_part_is_named_as_partly_searched()
    {
        var words = CoverageWords.Sentence(View(node =>
        {
            EverythingSearched(node);
            node["coverage"]!["intent"]!["covered"] = 0;
        }), exact: false);

        Assert.Equal("Searched product, code and function names. Intent is only partly searched.", words);
    }

    [Fact]
    public void Every_area_searched_says_nothing() =>
        Assert.Equal(string.Empty, CoverageWords.Sentence(View(EverythingSearched), exact: false));

    [Fact]
    public void An_exact_hit_says_nothing_even_when_areas_were_skipped() =>
        Assert.Equal(string.Empty, CoverageWords.Sentence(View(OneSearchedThreeNot), exact: true));
}
