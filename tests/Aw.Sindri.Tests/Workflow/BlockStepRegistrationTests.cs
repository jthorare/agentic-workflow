using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Tests.Workflow.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>AddBlockStep (DT spec §8.2): a block Step is found unkeyed (what contract tests enumerate), keyed by its id (what the activity runs) and by its registration; a descriptor that isn't a block one is refused.</summary>
[Lane(TestLane.Unit)]
public sealed class BlockStepRegistrationTests
{
    [Fact]
    public void A_block_Step_is_found_unkeyed_keyed_and_as_a_registration()
    {
        using var provider = new ServiceCollection().AddBlockStep<Judge>().BuildServiceProvider();
        using var scope = provider.CreateScope();

        _ = Assert.IsType<Judge>(Assert.Single(scope.ServiceProvider.GetServices<IBlockStep>()));
        _ = Assert.IsType<Judge>(scope.ServiceProvider.GetRequiredKeyedService<IBlockStep>("step.judge.choose"));
        Assert.Equal([BlockId.From("step.judge.choose")], provider.GetServices<BlockStepRegistration>().Select(r => r.Step));
    }

    [Fact]
    public void A_Step_whose_descriptor_is_not_a_block_one_is_refused_at_registration()
    {
        var services = new ServiceCollection();

        var refused = Assert.Throws<InvalidOperationException>(() => { _ = services.AddBlockStep<DeskTwin>(); });

        Assert.Contains("step.judge.choose", refused.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    private sealed class Judge : IBlockStep, IDescribedBlock
    {
        public static BlockDescriptor Descriptor { get; } = EngineFixture.Catalog.Find(BlockId.From("step.judge.choose"))!;

        public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) => ValueTask.FromResult(BlockStepResult.Failed("unused"));
    }

    private sealed class DeskTwin : IBlockStep, IDescribedBlock
    {
        public static BlockDescriptor Descriptor { get; } = Judge.Descriptor with { Runtime = BlockRuntime.Desk };

        public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) => ValueTask.FromResult(BlockStepResult.Failed("unused"));
    }
}
