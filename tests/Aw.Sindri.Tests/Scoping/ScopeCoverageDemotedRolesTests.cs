using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>
/// #1155, judge points 2 and 3: the demoted mark travels the real way (the repo's demoted roles flag the files, the host's JSON options write the lenses view, the gather reads it) and SCP-COV-003
/// then skips a script and a fixture the profile demotes and a test, while a product file still raises it.
/// </summary>
public sealed class ScopeCoverageDemotedRolesTests
{
    private const string Anchor = "surface:route/links-create";
    private const string Script = "scripts/seed.ts";
    private const string Fixture = "tests/fixtures/links.json";
    private const string Product = "src/links.ts";
    private const string Cov3 = "SCP-COV-003";

    private static readonly Dictionary<string, string> _roleNames = new(StringComparer.Ordinal) { ["scripts/**"] = "script", ["tests/fixtures/**"] = "fixture" };

    private static readonly NodeKey _anchor = NodeKey.From(Anchor);

    private static LensFile File(string path) => new(path, [_anchor], [], Provenance.Deterministic, 1.0, Confirmed: true, Suggested: false, LinkId: null, path, NodeKind.Code, Trust.Untrusted);

    /// <summary>A real lenses view whose code band is flagged by the real <see cref="DemotedFiles"/> over a repo that demotes the script and fixture roles, written with the host's JSON options.</summary>
    private static string LensesJson(params string[] demoteGlobs)
    {
        var repo = new ContextRepo(ContextRepoName.From("web"), "/work/web") { DemotePaths = demoteGlobs, Roles = [.. demoteGlobs.Select(static g => new RepoRole(_roleNames.GetValueOrDefault(g, "demo"), Demoted: true, [g]))] };
        var band = DemotedFiles.Last(new CodeBand([File(Product), File(Script), File(Fixture)], LensSymbols.Unavailable, new CodeCounts(3, 0, 3)), [repo]);
        var view = new LensesView(1, new LensAnchor(_anchor, "Create link", NodeKind.Surface, Trust.Trusted), "v1", Draft: false, 2, DateTimeOffset.UnixEpoch,
            new LensBands(
                new IntentBand([], [], new IntentCounts(0, 0, Orphaned: null)),
                new ProductBand([], [], [], new ProductCounts(0, 0), []),
                band),
            [], [],
            new LensCoverage(3, new CoverageIntent(0, 3), new CoverageProduct(0, 3), new CoverageCode(3, 3, "file", 0), []),
            new LensSearched([], "v1", 2, [], [], new SearchedBands(new BandSearched(Empty: false), new BandSearched(Empty: false), new BandSearched(Empty: false))),
            [], new Dictionary<string, Citation>(StringComparer.Ordinal));
        return JsonSerializer.Serialize(view, ShippedComposition.Json);
    }

    private static async Task<IReadOnlyList<string>> Cov3Items(string lensesJson, string? codeJson = null)
    {
        var gathered = LensGather.Assemble(JsonNode.Parse(lensesJson), codeJson is null ? null : JsonNode.Parse(codeJson), history: null, [Anchor]);
        var facts = ScopingFacts.Expiry().Edit("lenses", n => n["code"] = gathered.Lenses["code"]!.DeepClone()).Edit("scopeMap", ClearCitedLensItems);

        var response = (await new ScopingCheckService().Handle(new ScopingCheckRequest(facts, ScopeStage.Evidence), TestContext.Current.CancellationToken)).Value;

        return [.. response.Findings.Where(static f => string.Equals(f.Rule, Cov3, StringComparison.Ordinal)).Select(static f => f.Item).OfType<string>()];
    }

    // The expiry map cites L-code-1.. for its own surfaces and the gathered nodes reuse those ids, so without this every node would count as mapped.
    private static void ClearCitedLensItems(JsonNode map)
    {
        foreach (var surface in map["surfaces"]!.AsArray())
        {
            surface!["citations"] = new JsonArray();
        }

        map["excluded"] = new JsonArray();
    }

    [Fact, Trait("Lane", "unit")]
    public void The_hosts_lenses_json_carries_the_demoted_flag_of_the_demoted_files_only()
    {
        var files = JsonNode.Parse(LensesJson("scripts/**", "tests/fixtures/**"))!["bands"]!["code"]!["files"]!.AsArray();

        Assert.Equal([Script, Fixture], files.Where(static f => f!["demoted"]?.GetValue<bool>() == true).Select(static f => f!["path"]!.GetValue<string>()).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Null(files.Single(static f => f!["path"]!.GetValue<string>() == Product)!["demoted"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_gathered_nodes_carry_the_name_of_the_role_that_demotes_them()
    {
        var gathered = LensGather.Assemble(JsonNode.Parse(LensesJson("scripts/**", "tests/fixtures/**")), code: null, history: null, [Anchor]);

        var nodes = gathered.Lenses["code"]!["nodes"]!.AsArray();
        Assert.Equal("script", nodes.Single(static n => n!["key"]!.GetValue<string>() == $"code:{Script}")!["role"]!.GetValue<string>());
        Assert.Equal("fixture", nodes.Single(static n => n!["key"]!.GetValue<string>() == $"code:{Fixture}")!["role"]!.GetValue<string>());
        Assert.Null(nodes.Single(static n => n!["key"]!.GetValue<string>() == $"code:{Product}")!["role"]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_script_and_a_fixture_the_profile_demotes_raise_no_COV_003_through_the_real_path_and_a_product_file_does()
    {
        var items = await Cov3Items(LensesJson("scripts/**", "tests/fixtures/**"));

        Assert.DoesNotContain($"code:{Script}", items, StringComparer.Ordinal);
        Assert.DoesNotContain($"code:{Fixture}", items, StringComparer.Ordinal);
        Assert.Contains($"code:{Product}", items, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_ordinary_unmapped_file_next_to_an_anchor_still_raises_COV_003_when_no_demote_glob_matches_it()
    {
        var items = await Cov3Items(LensesJson("docs/**"));   // why: the repo demotes a role none of the three files belongs to

        Assert.Equal([$"code:{Script}", $"code:{Product}", $"code:{Fixture}"], items.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_near_node_tagged_with_any_role_raises_no_COV_003_even_when_it_is_not_demoted_and_an_ordinary_one_does()
    {
        const string Tagged = "code:src/tagged.ts";
        const string Plain = "code:src/plain.ts";
        var facts = ScopingFacts.Expiry().Edit("scopeMap", ClearCitedLensItems).Edit("lenses", n =>
        {
            var code = n["code"]!;
            foreach (var (key, role, item) in new (string, string?, string)[] { (Tagged, "fixture", "L-code-2001"), (Plain, null, "L-code-2002") })
            {
                var node = new JsonObject { ["key"] = key, ["kind"] = "file", ["label"] = key, ["band"] = "code", ["lensItem"] = item };
                if (role is not null)
                {
                    node["role"] = role;
                }

                code["nodes"]!.AsArray().Add(node);
                code["edges"]!.AsArray().Add(new JsonObject { ["from"] = key, ["to"] = Anchor, ["type"] = "implements", ["provenance"] = "deterministic", ["confirmed"] = true });
            }
        });

        var response = (await new ScopingCheckService().Handle(new ScopingCheckRequest(facts, ScopeStage.Evidence), TestContext.Current.CancellationToken)).Value;

        var items = response.Findings.Where(static f => string.Equals(f.Rule, Cov3, StringComparison.Ordinal)).Select(static f => f.Item).OfType<string>().ToList();
        Assert.DoesNotContain(Tagged, items, StringComparer.Ordinal);
        Assert.Contains(Plain, items, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_test_of_a_code_view_raises_no_COV_003()
    {
        var items = await Cov3Items(
            LensesJson(),
            $$"""{ "tests": [ { "path": "src/links.test.ts" } ], "edges": [ { "from": "code:src/links.test.ts", "to": "{{Anchor}}", "type": "tests" } ] }""");

        Assert.DoesNotContain("code:src/links.test.ts", items, StringComparer.Ordinal);
    }
}
