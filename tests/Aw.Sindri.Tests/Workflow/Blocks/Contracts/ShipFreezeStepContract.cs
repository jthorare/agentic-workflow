using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Ship;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The freeze Step on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class ShipFreezeStepContract : BlockContract
{
    protected override Type Block => typeof(ShipFreezeStep);

    protected override BlockDescriptor Descriptor => ShipSteps.Freeze;

    protected override string ExpectedHash => "sha256:3453040d172cde0b45d927abd0eafa80b9f37fe4fcaa4c2ad67705ed5d2840f6";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ShipFreezeStep(new FakeRunDiff(), new InMemoryPackets()).Run(BuildShipContexts.Of("step.ship.freeze", []), cancellationToken);
}
