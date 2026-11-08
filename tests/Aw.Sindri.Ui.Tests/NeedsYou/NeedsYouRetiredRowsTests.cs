using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>A server that still sends rows of the retired recipe kinds, or the retired fields on a kept kind, never makes the list fail (v2 PR 4).</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouRetiredRowsTests
{
    private static string Row(string id, string kind, string extra = "") =>
        $$"""{"id":"{{id}}","kind":"{{kind}}","label":"{{kind}}","subject":null,"item":null,"title":"T {{id}}","reason":"r","repo":null,"urgency":"calm","since":"2026-11-01T10:00:00+00:00","recordedOnly":false,"notice":null,"answerKind":null,"primaryAction":null,"actions":[],"cli":"sindri x"{{extra}}}""";

    private static NeedsYouFeed Feed(params string[] rows)
    {
        var body = "{\"rows\":[" + string.Join(',', rows) + "],\"empty\":null,\"asOf\":{\"cursor\":1,\"ledgerHead\":1,\"projectedAt\":null,\"rebuilding\":false,\"stalled\":false}}";
        var list = AwJson.Deserialize<NeedsYouList>(Encoding.UTF8.GetBytes(body), SindriApiClient.Json).Value;
        var catalog = AwJson.Deserialize<CommandList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("commands", "catalog")), SindriApiClient.Json).Value;
        return NeedsYouMapper.Map(list, catalog, decisions: null, new HomeCounts(0, 0, 0));
    }

    [Fact]
    public void Rows_of_a_retired_kind_are_dropped_and_the_others_still_show()
    {
        var feed = Feed(Row("a", "failure"), Row("b", "desk"), Row("c", "park"), Row("d", "skillWaiting"), Row("e", "recipe"), Row("f", "profile"));

        Assert.Equal(["a", "f"], feed.Rows.Select(r => r.Id), StringComparer.Ordinal);
    }

    [Fact]
    public void A_row_that_still_carries_the_retired_fields_parses_and_shows()
    {
        var retired = ",\"via\":\"child\",\"child\":\"ITM-1/x\",\"stop\":{\"n\":1,\"of\":3,\"state\":\"review\"},\"timeout\":{\"kind\":\"parks\",\"inSeconds\":60,\"on\":\"x\"},\"ownerWaiting\":{\"launchId\":\"L\",\"reason\":\"r\",\"text\":null,\"resultPath\":\"p\",\"detail\":null,\"instance\":\"i\",\"state\":\"s\"}";

        var feed = Feed(Row("a", "failure", retired), Row("b", "park", retired));

        Assert.Equal(["a"], feed.Rows.Select(r => r.Id), StringComparer.Ordinal);
    }
}
