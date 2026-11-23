using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>The 'rule_catalog' block (issue 3787).</summary>
[Lane(TestLane.Unit)]
public sealed class RuleCatalogBlockTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task It_hands_on_every_rule_with_its_checks_the_count_and_the_sections()
    {
        var result = await new RuleCatalogBlock(new FakeRuleCatalog(
        [
            new RuleCatalogSeed("engineering.csharp.b-rule", "4b.2", "Second rule text.", [new RuleCheckListing("test", "BTests")]),
            new RuleCatalogSeed("engineering.csharp.a-rule", "4b.1", "First rule text.", []),
            new RuleCatalogSeed("engineering.csharp.c-rule", "4b.2", "Third rule text.", []),
        ])).Execute(BlockFakes.Context([]), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        var rules = result.Value.Outputs["rules"]!.AsArray();
        Assert.Equal(["engineering.csharp.a-rule", "engineering.csharp.b-rule", "engineering.csharp.c-rule"], rules.Select(static r => r!["id"]!.GetValue<string>()));
        Assert.Equal(["id", "kind", "section", "text", "status", "checks"], rules[1]!.AsObject().Select(static p => p.Key));
        Assert.Equal(("test", "BTests"), (rules[1]!["checks"]![0]!["type"]!.GetValue<string>(), rules[1]!["checks"]![0]!["id"]!.GetValue<string>()));
        Assert.Equal(3, result.Value.Outputs["count"]!.GetValue<int>());
        Assert.Equal(0, result.Value.Outputs["skipped"]!.GetValue<int>());
        Assert.Equal(["4b.1", "4b.2"], result.Value.Outputs["sections"]!.AsArray().Select(static s => s!.GetValue<string>()));
    }

    [Fact]
    public async Task A_long_text_is_cut_so_the_list_stays_small()
    {
        var result = await new RuleCatalogBlock(new FakeRuleCatalog([new RuleCatalogSeed("engineering.csharp.long", "4b.1", new string('t', 2000), [])])).Execute(BlockFakes.Context([]), Token);

        Assert.Equal(RuleCatalogBlock.TextChars, result.Value.Outputs["rules"]![0]!["text"]!.GetValue<string>().Length);
    }

    [Fact]
    public async Task No_more_than_the_cap_of_rules_is_handed_on_in_id_order()
    {
        var seeds = Enumerable.Range(0, RuleCatalogBlock.MaxRules + 20).Select(static i => new RuleCatalogSeed($"engineering.csharp.rule-{i:D4}", "4b.1", "A rule text.", [])).ToList();

        var result = await new RuleCatalogBlock(new FakeRuleCatalog(seeds)).Execute(BlockFakes.Context([]), Token);

        Assert.Equal(RuleCatalogBlock.MaxRules, result.Value.Outputs["rules"]!.AsArray().Count);
        Assert.Equal(RuleCatalogBlock.MaxRules, result.Value.Outputs["count"]!.GetValue<int>());
        Assert.Equal("engineering.csharp.rule-0499", result.Value.Outputs["rules"]![RuleCatalogBlock.MaxRules - 1]!["id"]!.GetValue<string>());
    }

    [Fact]
    public async Task The_skipped_count_of_the_catalog_is_handed_on()
    {
        var result = await new RuleCatalogBlock(new SkippingCatalog()).Execute(BlockFakes.Context([]), Token);

        Assert.Equal(2, result.Value.Outputs["skipped"]!.GetValue<int>());
        Assert.Equal(0, result.Value.Outputs["count"]!.GetValue<int>());
    }

    [Fact]
    public async Task A_catalog_that_can_t_be_read_fails_the_node_with_its_reason()
    {
        var result = await new RuleCatalogBlock(new FailingCatalog()).Execute(BlockFakes.Context([]), Token);

        Assert.True(result.IsError);
        Assert.Contains("unreadable", result.Error.Message, StringComparison.Ordinal);
    }

    private sealed class SkippingCatalog : IRuleCatalogPort
    {
        public ValueTask<Result<RuleCatalogListing>> List(CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success(new RuleCatalogListing([], Skipped: 2)));
    }

    private sealed class FailingCatalog : IRuleCatalogPort
    {
        public ValueTask<Result<RuleCatalogListing>> List(CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Failure<RuleCatalogListing>(SindriErrors.GraphBlockFailed.With("the rules folder is unreadable")));
    }
}
