using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (the design station, BS2b): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class DesignIntakeStepContract : BlockContract
{
    protected override Type Block => typeof(DesignIntakeStep);

    protected override BlockDescriptor Descriptor => DesignSteps.Intake;

    protected override string ExpectedHash => "sha256:fc0e7bb00676667d29486e193b739963e88051e65efd36b237ab1c6bb6c2f17a";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new DesignIntakeStep(new InMemoryPackets(), new InMemoryPacketFiles()).Run(ScopingStepContexts.Of("step.design.intake", []), cancellationToken);
}
