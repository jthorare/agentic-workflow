using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Host;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The blocks and events this build ships (spec §12), read from the real composition.</summary>
public sealed class ShippedBlocksTests
{
    private static readonly string _stateRoot = Path.Combine(Path.GetTempPath(), "aw-sindri-blocks-test");

    [Fact, Trait("Lane", "unit")]
    public async Task The_catalog_holds_exactly_the_section_12_blocks_and_events()
    {
        await using var provider = Composed();
        var catalog = provider.GetRequiredService<BlockCatalog>();

        Assert.Equal(
            ["effect.claim.ref", "effect.notify.desk", "gate.budget.available", "gate.route.startable", "step.checks", "step.produce", "step.verify"],
            catalog.Blocks.Select(b => b.Id.Value).Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
        Assert.Equal(["claim.created", "item.routed", "notify.delivered"], catalog.Events.Select(e => e.Kind.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Only_the_budget_gate_has_a_runtime_and_the_desk_steps_run_on_the_desk()
    {
        await using var provider = Composed();
        await using var scope = provider.CreateAsyncScope();

        _ = Assert.IsType<BudgetAvailableGate>(Assert.Single(scope.ServiceProvider.GetServices<IGate>()));
        Assert.Empty(scope.ServiceProvider.GetServices<IStep>());
        Assert.Empty(scope.ServiceProvider.GetServices<IEffectKind>());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("step.produce", new[] { "done" }, "fact:output", new[] { "done" })]
    [InlineData("step.checks", new[] { "passed", "failed" }, "fact:findings", new[] { "failed" })]
    [InlineData("step.verify", new[] { "accepted", "rejected" }, "fact:findings", new[] { "rejected" })]
    public void Each_desk_step_declares_its_outcomes_and_required_output(string id, string[] outcomes, string output, string[] requiredOn)
    {
        var step = FixtureCatalog.Shipped().Find(BlockId.From(id))!;   // why: the catalog test pins every shipped id

        Assert.True(step.DeskCapable);
        Assert.Equal(outcomes, step.Outcomes.Select(o => o.Value), StringComparer.Ordinal);
        var spec = Assert.Single(step.Outputs);
        Assert.Equal(output, spec.Fact.Value);
        Assert.Equal(requiredOn, spec.RequiredOn.Select(o => o.Value), StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("effect.claim.ref", Capability.Effects | Capability.WriteScope)]
    [InlineData("effect.notify.desk", Capability.Effects)]
    [InlineData("gate.budget.available", Capability.None)]
    [InlineData("gate.route.startable", Capability.None)]
    public void Each_block_declares_what_it_reaches(string id, Capability capabilities) =>
        Assert.Equal(capabilities, FixtureCatalog.Shipped().Find(BlockId.From(id))!.Capabilities);   // why: the catalog test pins every shipped id

    [Theory, Trait("Lane", "unit")]
    [InlineData("item.routed", "router.stub", EventRoute.Item, new[] { "startable", "notStartable" }, new[] { "fact:stepSpec" })]
    [InlineData("claim.created", "system:dispatcher", EventRoute.Causation, new[] { "created", "claimExists" }, new string[0])]
    [InlineData("notify.delivered", "system:dispatcher", EventRoute.Causation, new[] { "delivered" }, new string[0])]
    public void Each_event_declares_its_producer_route_outcomes_and_facts(string kind, string producer, EventRoute route, string[] outcomes, string[] facts)
    {
        var e = FixtureCatalog.Shipped().FindEvent(EventKind.From(kind))!;   // why: the catalog test pins every shipped kind

        Assert.Equal(producer, e.Producer);
        Assert.Equal(route, e.Route);
        Assert.Equal(outcomes, e.Outcomes.Select(o => o.Value), StringComparer.Ordinal);
        Assert.Equal(facts, e.Facts.Select(f => f.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_descriptor_kind_matches_its_id_prefix()
    {
        var blocks = FixtureCatalog.WithFixtures().Blocks;

        Assert.NotEmpty(blocks);
        Assert.All(blocks, b => Assert.Equal(BlockKinds.Of(b.Id), b.Kind));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_gate_has_a_rung_and_no_other_block_does()
    {
        var blocks = FixtureCatalog.WithFixtures().Blocks;

        Assert.All(blocks.Where(b => b.Kind == BlockKind.Gate), b => Assert.NotNull(b.Rung));
        Assert.All(blocks.Where(b => b.Kind != BlockKind.Gate), b => Assert.Null(b.Rung));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_effect_is_capability_bearing() =>
        Assert.All(FixtureCatalog.WithFixtures().Blocks.Where(b => b.Kind == BlockKind.Effect), b => Assert.True(b.Capabilities.HasFlag(Capability.Effects)));

    private static ServiceProvider Composed() =>
        Composition.Compose(new ServiceCollection(), name => name == "AW_STATE_DIR" ? _stateRoot : null, new FakeTimeProvider())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
}
