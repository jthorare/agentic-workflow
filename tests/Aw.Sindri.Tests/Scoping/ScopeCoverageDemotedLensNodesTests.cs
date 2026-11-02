using System.Text.Json.Nodes;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>
/// #1155: SCP-COV-003 skips the files the repo demotes and the tests next to an anchor. The gather copies the context engine's demoted flag and the
/// test role onto the lens node, so the check's filter has something to read; an ordinary unmapped file next to an anchor still fails.
/// </summary>
public sealed class ScopeCoverageDemotedLensNodesTests
{
    private const string Anchor = "surface:route/links-create";
    private const string Cov3 = "SCP-COV-003";

    private static async Task<IReadOnlyList<string>> Cov3Items(string lensesView, string codeView)
    {
        var gathered = LensGather.Assemble(JsonNode.Parse(lensesView), JsonNode.Parse(codeView), history: null, [Anchor]);
        var facts = ScopingFacts.Expiry().Edit("lenses", n => n["code"] = gathered.Lenses["code"]!.DeepClone())
            .Edit("scopeMap", ClearCitedLensItems);

        var response = (await new ScopingCheckService().Handle(new ScopingCheckRequest(facts, ScopeStage.Evidence), TestContext.Current.CancellationToken)).Value;

        return [.. response.Findings.Where(static f => string.Equals(f.Rule, Cov3, StringComparison.Ordinal)).Select(static f => f.Item).OfType<string>()];
    }

    // The expiry map cites L-code-1.. for its own surfaces; the gathered nodes reuse those ids, so without this every node would count as mapped.
    private static void ClearCitedLensItems(JsonNode map)
    {
        foreach (var surface in map["surfaces"]!.AsArray())
        {
            surface!["citations"] = new JsonArray();
        }

        map["excluded"] = new JsonArray();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_demoted_file_next_to_an_anchor_raises_no_COV_003()
    {
        var items = await Cov3Items(
            """{ "anchors": [ { "key": "surface:route/links-create" } ], "bands": { "code": { "files": [ { "path": "scripts/seed.ts", "implements": [ "surface:route/links-create" ], "demoted": true } ] } } }""",
            """{ "files": [] }""");

        Assert.DoesNotContain("code:scripts/seed.ts", items, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_test_next_to_an_anchor_raises_no_COV_003()
    {
        var items = await Cov3Items(
            """{ "anchors": [ { "key": "surface:route/links-create" } ] }""",
            """{ "tests": [ { "path": "src/links.test.ts" } ], "edges": [ { "from": "code:src/links.test.ts", "to": "surface:route/links-create", "type": "tests" } ] }""");

        Assert.DoesNotContain("code:src/links.test.ts", items, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_ordinary_unmapped_file_next_to_an_anchor_still_raises_COV_003()
    {
        var items = await Cov3Items(
            """{ "anchors": [ { "key": "surface:route/links-create" } ], "bands": { "code": { "files": [ { "path": "src/links.ts", "implements": [ "surface:route/links-create" ] } ] } } }""",
            """{ "files": [] }""");

        Assert.Contains("code:src/links.ts", items, StringComparer.Ordinal);
    }
}
