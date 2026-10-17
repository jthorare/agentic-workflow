using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Capabilities;

namespace Aw.Sindri.Tests.Workflow.InvalidFixtures;

/// <summary>
/// Every invalid fixture end to end (spec §9.1: each rule has an invalid fixture): through recipe validate's service,
/// against the setup the fixture is meant for. One fault gives one finding, so each fixture reports its own rule
/// exactly once, at the position its rule-level test states, and every other finding it gets is listed in its case.
/// </summary>
public sealed class InvalidFixturesTests
{
    /// <summary>Spec §9.1's rules.</summary>
    private static readonly string[] _section91 =
    [
        "RCP-SCHEMA-001", "RCP-SCHEMA-002", "RCP-SCHEMA-003", "RCP-STATE-001", "RCP-INIT-001", "RCP-DUR-001", "RCP-NAME-001",
        "RCP-BLOCK-001", "RCP-BLOCK-002", "RCP-BLOCK-003", "RCP-EVID-001", "RCP-EVENT-001",
        "RCP-WIRE-001", "RCP-WIRE-002", "RCP-WHEN-001", "RCP-OUTCOME-001", "RCP-CHILD-001",
        "RCP-CAP-001", "RCP-CAP-002", "RCP-CAP-003", "RCP-CAP-004", "RCP-CAP-005", "RCP-CAP-006", "RCP-CAP-007",
        "RCP-REACH-001", "RCP-EXIT-001", "RCP-TIME-001", "RCP-LOOP-001", "RCP-LOOP-002", "RCP-LOOP-003", "RCP-EFFECT-001", "RCP-HASH-001",
        "RCP-TRIG-001", "RCP-WHEN-002", "RCP-THRESH-001", "RCP-THRESH-002", "RCP-NEXT-001", "RCP-NEXT-002", "RCP-CHILD-002",
        "RCP-CHILD-003", "RCP-VERSION-001", "RCP-VERSION-002", "RCP-TRIG-002", "RCP-TIME-002", "RCP-RES-001",
        "RCP-URG-001", "RCP-OUT-001", "RCP-OUT-002", "RCP-OUT-003", "RCP-TYPE-001", "RCP-TYPE-002", "RCP-TYPE-003",
        "RCP-INPUT-001", "RCP-INPUT-002", "RCP-NAME-002", "RCP-NAME-003", "RCP-NAME-004", "RCP-NAME-005", "RCP-NAME-006",
    ];

    /// <summary>The decision-tree rules (DT spec §3.2, §8.1).</summary>
    private static readonly string[] _decisionTree =
    [
        "RCP-CHOICE-001", "RCP-CHOICE-002", "RCP-CHOICE-003", "RCP-CHOICE-004", "RCP-CHOICE-005", "RCP-CHOICE-006",
        "RCP-TIME-003",
    ];

    /// <summary>The child-output rule (DT spec §8.4).</summary>
    private static readonly string[] _childOutputs = ["RCP-WIRE-003"];

    /// <summary>Fixtures that are a second fault of a rule, named after it: RCP-CHOICE-002's unquoted entry, RCP-CHOICE-005's computed twin source, and a route without an uncertain transition.</summary>
    private static readonly string[] _variants = ["RCP-CHOICE-002-mapping", "RCP-CHOICE-005-computed", "RCP-OUTCOME-001-uncertain", "RCP-SCHEMA-002-camel", "RCP-WIRE-003-cancelled", "RCP-WIRE-002-child", "RCP-WIRE-002-rename", "RCP-SCHEMA-001-produces", "RCP-TIME-003-short"];

    public static TheoryData<string> Rules => [.. InvalidFixtureCases.All.Keys.Order(StringComparer.Ordinal)];

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Rules))]
    public async Task Each_invalid_fixture_reports_its_rule_once_at_its_position_and_nothing_unlisted(string fixture)
    {
        var expected = InvalidFixtureCases.All[fixture];
        var rule = string.Join('-', fixture.Split('-').Take(3));

        var lines = await InvalidFixtureRun.Lines(fixture, expected.Setup, TestContext.Current.CancellationToken);

        var own = Assert.Single(lines, l => l.StartsWith(rule + ": ", StringComparison.Ordinal));
        Assert.Equal(expected.Own, own);
        Assert.Equal(expected.Others, lines.Where(l => !ReferenceEquals(l, own)), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_rule_has_an_invalid_fixture_and_an_end_to_end_case()
    {
        var fixtures = typeof(RecipeFixtures).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith("recipes/invalid/", StringComparison.Ordinal))
            .Select(n => Path.GetFileNameWithoutExtension(n))
            .Order(StringComparer.Ordinal);

        Assert.Equal(_section91.Concat(_decisionTree).Concat(_childOutputs).Concat(_variants).Order(StringComparer.Ordinal), fixtures, StringComparer.Ordinal);
        Assert.Equal(fixtures, InvalidFixtureCases.All.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_capability_fixtures_name_the_base_fixture_by_its_hash()
    {
        var baseHash = InvalidFixtureRun.Base(RecipeReaderTests.Reader).Hash;
        var named = InvalidFixtureCases.All.Keys
            .Select(rule => RecipeReaderTests.Reader.Read(RecipeFixtures.Read($"invalid/{rule}.yaml"), rule + ".yaml").Document?.Recipe.Base)
            .Append(RecipeReaderTests.Reader.Read(RecipeFixtures.Read("valid/workflow.cap-generated.yaml"), "g.yaml").Document?.Recipe.Base)
            .OfType<Core.Workflow.Recipes.RecipeRef>()
            .ToList();

        Assert.Equal(CapabilityRecipe.BaseHash, baseHash);
        Assert.Equal(7, named.Count);
        Assert.All(named, r => Assert.Equal(baseHash, r.Hash));
    }
}
