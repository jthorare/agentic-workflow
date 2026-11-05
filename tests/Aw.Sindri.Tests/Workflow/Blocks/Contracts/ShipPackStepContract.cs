using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Ship;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Prove;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The pack Step on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class ShipPackStepContract : BlockContract
{
    protected override Type Block => typeof(ShipPackStep);

    protected override BlockDescriptor Descriptor => ShipSteps.Pack;

    protected override string ExpectedHash => "sha256:7343891bbe7baf063e2663110b37fd2ef2287d1f54bf4cde841f1c04dd619362";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ShipPackStep(new InMemoryPacketDirectory(), new InMemoryRepoCheckouts(), new InMemoryHoldoutStore(), new FakeRunDiff(), new PassThroughScrubber()).Run(BuildShipContexts.Of("step.ship.pack", []), cancellationToken);
}
