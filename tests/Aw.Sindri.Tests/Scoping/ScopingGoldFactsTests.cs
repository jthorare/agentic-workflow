using System.Text.Json.Nodes;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>
/// The gold facts and the line gold scope packet (tests/fixtures/line/gold/scope/expiry) are one document: the facts equal the packet with the
/// stamped fields removed, so neither drifts alone. Every gold scope packet (expiry and archive, discovered) passes the strict map and acceptance rules.
/// </summary>
public sealed class ScopingGoldFactsTests
{
    private static readonly string _scopes = Path.Combine(RepoRoot(), "tests", "fixtures", "line", "gold", "scope");
    private static readonly string _gold = Path.Combine(_scopes, "expiry");

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("AgenticWorkflow.slnx not found above the test assembly");
    }

    private static readonly string[] _stamped = ["trust", "schemaVersion"];

    private static JsonNode? Strip(JsonNode? node, bool top)
    {
        if (node is JsonObject o)
        {
            foreach (var name in _stamped)
            {
                _ = o.Remove(name);
            }

            if (top)
            {
                _ = o.Remove("change");   // the packet names its change and base commit; the fact form refuses both
                _ = o.Remove("baseSha");
            }

            if (o["parent"] is null)
            {
                _ = o.Remove("parent");   // a null parent is how the packet says "none"
            }

            foreach (var (_, child) in o.ToList())
            {
                _ = Strip(child, top: false);
            }
        }
        else if (node is JsonArray a)
        {
            foreach (var child in a)
            {
                _ = Strip(child, top: false);
            }
        }

        return node;
    }

    [Theory, Trait("Lane", "integration")]   // reads the line fixture from the source tree
    [InlineData("scopeMap", "scope-map.json")]
    [InlineData("acceptance", "acceptance.json")]
    [InlineData("coverage", "coverage.json")]
    [InlineData("challenge", "challenge.json")]
    [InlineData("lenses", "lenses.json")]
    [InlineData("relevance", "relevance.json")]
    [InlineData("gapSearch", "gap-search.json")]
    public async Task The_facts_equal_the_line_gold_packet_without_its_stamped_fields(string fact, string file)
    {
        var gold = Strip(JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(_gold, file), TestContext.Current.CancellationToken)), top: true)!;
        var facts = Strip(JsonNode.Parse(ScopingFacts.Expiry()[fact]), top: true)!;
        Assert.Equal(gold.ToJsonString(), facts.ToJsonString());
    }

    /// <summary>Every directory under tests/fixtures/line/gold/scope, so a new gold packet is checked without a test edit.</summary>
    public static IEnumerable<string> Discovered() => Directory.EnumerateDirectories(_scopes).Select(static d => Path.GetFileName(d)).Order(StringComparer.Ordinal);

    /// <summary>The same, as theory rows.</summary>
    public static TheoryData<string> Changes() => [.. Discovered()];

    [Fact, Trait("Lane", "integration")]
    public void The_discovery_finds_the_known_gold_packets()
    {
        Assert.Contains("expiry", Discovered(), StringComparer.Ordinal);
        Assert.Contains("archive", Discovered(), StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "integration")]   // reads the line fixture from the source tree
    [MemberData(nameof(Changes))]
    public async Task Every_gold_scope_packet_passes_the_strict_map_and_acceptance_rules(string change)
    {
        var facts = new ScopingFacts();
        foreach (var (key, file) in new[] { ("scopeMap", "scope-map.json"), ("acceptance", "acceptance.json") })
        {
            var text = await File.ReadAllTextAsync(Path.Combine(_scopes, change, file), TestContext.Current.CancellationToken);
            facts[key] = Strip(JsonNode.Parse(text), top: true)!.ToJsonString();
        }

        var response = (await new ScopingCheckService().Handle(new ScopingCheckRequest(facts, ScopeStage.Draft), CancellationToken.None)).Value;

        Assert.Empty(response.Findings);
        Assert.Equal(ScopeRuleStatus.Pass, response.Rules.Single(r => r.Rule == "SCP-MAP-001").Status);
        Assert.Equal(ScopeRuleStatus.Pass, response.Rules.Single(r => r.Rule == "SCP-ACC-002").Status);
    }
}
