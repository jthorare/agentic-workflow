using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow;

public sealed class BlockCatalogTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_catalog_is_built_from_the_block_and_event_registrations()
    {
        var descriptorOnly = TestBlocks.Gate("gate.route.startable");
        using var provider = new ServiceCollection()
            .AddSingleton<BlockCatalog>()
            .AddGate<FakeGate>()
            .AddStep<FakeStep>()
            .AddBlockDescriptor(descriptorOnly)
            .AddEvent(TestBlocks.Event("item.routed", "startable", "notStartable"))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        var catalog = provider.GetRequiredService<BlockCatalog>();

        Assert.Same(FakeGate.Descriptor, catalog.Find(BlockId.From("gate.test.pass")));
        Assert.Same(FakeStep.Descriptor, catalog.Find(BlockId.From("step.test.produce")));
        Assert.Same(descriptorOnly, catalog.Find(BlockId.From("gate.route.startable")));
        Assert.Equal([OutcomeName.From("startable"), OutcomeName.From("notStartable")], catalog.FindEvent(EventKind.From("item.routed"))!.Outcomes);   // why: the event was registered above
        Assert.Equal(3, catalog.Blocks.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_registered_block_resolves_as_its_kind_interface()
    {
        using var provider = new ServiceCollection().AddGate<FakeGate>().AddStep<FakeStep>().BuildServiceProvider();
        using var scope = provider.CreateScope();

        _ = Assert.IsType<FakeGate>(Assert.Single(scope.ServiceProvider.GetServices<IGate>()));
        _ = Assert.IsType<FakeStep>(Assert.Single(scope.ServiceProvider.GetServices<IStep>()));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_block_or_event_is_null()
    {
        Assert.Null(BlockCatalog.Empty.Find(BlockId.From("step.produce")));
        Assert.Null(BlockCatalog.Empty.FindEvent(EventKind.From("item.routed")));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_block_registered_twice_is_a_bug() =>
        _ = Assert.Throws<InvalidOperationException>(() => new BlockCatalog([TestBlocks.Step("step.produce"), TestBlocks.Step("step.produce")], []));

    [Fact, Trait("Lane", "unit")]
    public void An_event_registered_twice_is_a_bug() =>
        _ = Assert.Throws<InvalidOperationException>(() => new BlockCatalog([], [TestBlocks.Event("item.routed"), TestBlocks.Event("item.routed")]));

    [Fact, Trait("Lane", "unit")]
    public void A_descriptor_whose_kind_isnt_its_prefix_is_a_bug() =>
        _ = Assert.Throws<InvalidOperationException>(() => new BlockCatalog([TestBlocks.Step("step.produce") with { Kind = BlockKind.Gate }], []));
}
