using System.Text.Json;
using Aw.Sindri.Host.Mcp;

namespace Aw.Sindri.Tests.Mcp;

/// <summary>One limit and offset for every list a tool returns. Buckets stay whole; their lists page.</summary>
public sealed class McpPagesTests
{
    [Fact, Trait("Lane", "unit")]
    public void Each_list_keeps_one_window_and_the_buckets_stay_whole()
    {
        var answer = Json("{\"sections\":[{\"name\":\"graph\",\"items\":[\"g0\",\"g1\"],\"more\":[\"C3\"]},{\"name\":\"records\",\"items\":[\"r0\"],\"more\":[]}],\"groups\":[{\"group\":\"queued\",\"items\":[\"a\",\"b\",\"c\"]},{\"group\":\"running\",\"items\":[]}],\"edges\":[\"e0\",\"e1\",\"e2\"],\"counts\":{\"edges\":3}}");

        var page = McpPages.Apply(answer, new McpWindow(Limit: 1, Offset: 0));

        Assert.Equal(["graph", "records"], Names(page, "sections"));
        Assert.Equal(["g0"], Items(page, "sections", 0));
        Assert.Equal(["queued", "running"], Names(page, "groups", "group"));
        Assert.Equal(["a"], Items(page, "groups", 0));
        Assert.Equal("e0", page.GetProperty("edges")[0].GetString());
        Assert.Equal(1, page.GetProperty("edges").GetArrayLength());
        Assert.Equal(3, page.GetProperty("counts").GetProperty("edges").GetInt32());
        Assert.Equal((0, 1, true), (page.GetProperty("page").GetProperty("offset").GetInt32(), page.GetProperty("page").GetProperty("limit").GetInt32(), page.GetProperty("page").GetProperty("more").GetBoolean()));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_next_offset_continues_and_an_offset_past_the_end_is_empty()
    {
        var answer = Json("""{ "edges": ["e0", "e1", "e2"] }""");

        var second = McpPages.Apply(answer, new McpWindow(Limit: 1, Offset: 1));
        var past = McpPages.Apply(answer, new McpWindow(Limit: 1, Offset: 5));

        Assert.Equal("e1", second.GetProperty("edges")[0].GetString());
        Assert.True(second.GetProperty("page").GetProperty("more").GetBoolean());
        Assert.Equal(0, past.GetProperty("edges").GetArrayLength());
        Assert.False(past.GetProperty("page").GetProperty("more").GetBoolean());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_page_is_an_optional_limit_of_1_or_more_and_an_optional_offset_of_0_or_more()
    {
        Assert.Equal(new McpWindow(McpWindow.DefaultLimit, 0), McpArguments.ReadPage(arguments: null).Value);
        Assert.Equal(new McpWindow(20, 0), McpArguments.ReadPage(Args("""{"limit":20}""")).Value);
        Assert.Equal(new McpWindow(20, 3), McpArguments.ReadPage(Args("""{"limit":20,"offset":3}""")).Value);
        Assert.Equal(new McpWindow(McpWindow.DefaultLimit, 1), McpArguments.ReadPage(Args("""{"offset":1}""")).Value);

        Assert.Equal("SND-CONTEXT-006", McpArguments.ReadPage(Args("""{"limit":0}""")).Error.Code);
        Assert.Equal("SND-CONTEXT-006", McpArguments.ReadPage(Args("""{"limit":-3}""")).Error.Code);
        Assert.Equal("SND-CONTEXT-006", McpArguments.ReadPage(Args("""{"offset":-1}""")).Error.Code);
        Assert.Equal("SND-CONTEXT-022", McpArguments.ReadPage(Args("""{"limit":"2"}""")).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Arrays_inside_an_item_are_never_sliced()
    {
        var answer = Json("""{ "sections": [{ "name": "graph", "items": [ {"id":"a","evidence":["e1","e2","e3"]}, {"id":"b","evidence":["e4","e5"]}, {"id":"c","evidence":["e6"]} ], "more": [] }], "edges": ["x", "y", "z"] }""");

        var page = McpPages.Apply(answer, new McpWindow(Limit: 1, Offset: 1));

        var items = page.GetProperty("sections")[0].GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("b", items[0].GetProperty("id").GetString());
        Assert.Equal(["e4", "e5"], items[0].GetProperty("evidence").EnumerateArray().Select(e => e.GetString()), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Each_top_level_list_has_its_own_window_so_a_short_list_does_not_move_the_others()
    {
        var answer = Json("""{ "conflicts": ["c0"], "suggestedAnchors": ["s0", "s1", "s2", "s3"], "degraded": [] }""");

        var first = McpPages.Apply(answer, new McpWindow(Limit: 2, Offset: 0));
        var second = McpPages.Apply(answer, new McpWindow(Limit: 2, Offset: 2));

        Assert.Equal(["c0"], Strings(first, "conflicts"));
        Assert.Equal(["s0", "s1"], Strings(first, "suggestedAnchors"));
        Assert.Empty(Strings(second, "conflicts"));
        Assert.Equal(["s2", "s3"], Strings(second, "suggestedAnchors"));
        Assert.True(first.GetProperty("page").GetProperty("more").GetBoolean());
        Assert.False(second.GetProperty("page").GetProperty("more").GetBoolean());
        var lists = first.GetProperty("page").GetProperty("lists");
        Assert.Equal((1, false, 4, true), (lists.GetProperty("conflicts").GetProperty("total").GetInt32(), lists.GetProperty("conflicts").GetProperty("more").GetBoolean(),
            lists.GetProperty("suggestedAnchors").GetProperty("total").GetInt32(), lists.GetProperty("suggestedAnchors").GetProperty("more").GetBoolean()));
    }

    [Fact, Trait("Lane", "unit")]
    public void Paging_a_query_answer_reaches_every_item_once_and_more_is_truthful()
    {
        var items = string.Join(',', Enumerable.Range(0, 250).Select(i => $"{{\"id\":\"i{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}\",\"evidence\":[\"a\",\"b\"]}}"));
        var answer = Json("{\"sections\":[{\"name\":\"graph\",\"items\":[" + items + "],\"more\":[]},{\"name\":\"records\",\"items\":[{\"id\":\"r0\"}],\"more\":[]}],\"truncated\":false}");

        var seen = new List<string>();
        var offset = 0;
        var pages = 0;
        while (true)
        {
            var page = McpPages.Apply(answer, new McpWindow(Limit: 100, Offset: offset));
            seen.AddRange(page.GetProperty("sections")[0].GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()!));
            pages++;
            if (!page.GetProperty("page").GetProperty("more").GetBoolean())
            {
                break;
            }

            offset += 100;
        }

        Assert.Equal(3, pages);
        Assert.Equal(Enumerable.Range(0, 250).Select(i => "i" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)), seen, StringComparer.Ordinal);
    }

    private static string[] Strings(JsonElement page, string list) =>
        [.. page.GetProperty(list).EnumerateArray().Select(item => item.GetString()!)];

    private static string[] Names(JsonElement page, string list, string property = "name") =>
        [.. page.GetProperty(list).EnumerateArray().Select(item => item.GetProperty(property).GetString()!)];

    private static string[] Items(JsonElement page, string list, int index) =>
        [.. page.GetProperty(list)[index].GetProperty("items").EnumerateArray().Select(item => item.GetString()!)];

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static Dictionary<string, JsonElement> Args(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    [Fact, Trait("Lane", "unit")]
    public void A_bucket_list_is_keyed_by_the_bucket_name_not_its_position()
    {
        var answer = Json("""{ "sections": [{ "name": "records", "items": [{"id":"r"}] }, { "name": "graph", "items": [{"id":"g"}] }] }""");

        var lists = McpPages.Apply(answer, new McpWindow(Limit: 1, Offset: 0)).GetProperty("page").GetProperty("lists");

        Assert.Equal(["sections.records.items", "sections.graph.items"], lists.EnumerateObject().Select(p => p.Name), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Degraded_sources_are_a_warning_and_survive_every_window()
    {
        var answer = Json("""{ "degraded": [{"source":"onyx"}, {"source":"linear"}], "suggestedAnchors": ["a", "b"] }""");

        var past = McpPages.Apply(answer, new McpWindow(Limit: 1, Offset: 1));

        Assert.Equal(2, past.GetProperty("degraded").GetArrayLength());
        Assert.False(past.GetProperty("page").GetProperty("lists").TryGetProperty("degraded", out _));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_page_keeps_only_the_citations_its_own_items_name()
    {
        var answer = Json("""{ "sections": [{ "name": "records", "items": [{"id":"a","cites":["C1"]}, {"id":"b","cites":["C2"]}] }], "cites": { "C1": {"k":1}, "C2": {"k":2} } }""");

        var second = McpPages.Apply(answer, new McpWindow(Limit: 1, Offset: 1));

        Assert.Equal(["C2"], second.GetProperty("cites").EnumerateObject().Select(c => c.Name), StringComparer.Ordinal);
    }
}
