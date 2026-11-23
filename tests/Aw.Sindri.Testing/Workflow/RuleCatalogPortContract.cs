using Aw.Sindri.Core.Graph.Blocks;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The contract every <see cref="IRuleCatalogPort"/> honours (issue 3787): every rule by id, sorted, with its fields and checks; a rule with no checks is listed with an empty list.</summary>
public abstract class RuleCatalogPortContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The port under test, over these rules.</summary>
    /// <param name="rules">The rules to seed.</param>
    /// <returns>The port.</returns>
    protected abstract ValueTask<IRuleCatalogPort> Create(IReadOnlyList<RuleCatalogSeed> rules);

    [Fact]
    public async Task Every_rule_is_listed_by_id_in_id_order_with_its_fields_and_checks()
    {
        var port = await Create(
        [
            new RuleCatalogSeed("engineering.csharp.zeta-rule", "4b.2", "Keep every file under 300 lines.", [new RuleCheckListing("test", "LineCountTests"), new RuleCheckListing("review", "no long files")]),
            new RuleCatalogSeed("engineering.csharp.alpha-rule", "4b.1", "Write one type per file.", []),
        ]);

        var listed = await port.List(Token);

        Expect.Sequence(["engineering.csharp.alpha-rule", "engineering.csharp.zeta-rule"], listed.Value.Rules.Select(static r => r.Id));
        var zeta = listed.Value.Rules[1];
        Expect.Equal(("engineering", "4b.2", "Keep every file under 300 lines.", "active"), (zeta.Kind, zeta.Section, zeta.Text, zeta.Status));
        Expect.Sequence([new RuleCheckListing("test", "LineCountTests"), new RuleCheckListing("review", "no long files")], zeta.Checks);
        Expect.Equal(0, listed.Value.Rules[0].Checks.Count);
        Expect.Equal(0, listed.Value.Skipped);
    }

    [Fact]
    public async Task No_rules_is_an_empty_list()
    {
        var port = await Create([]);

        var listed = await port.List(Token);

        Expect.Equal(0, listed.Value.Rules.Count);
        Expect.Equal(0, listed.Value.Skipped);
    }
}
