using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (scoping station, BS-1b): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeFreezeStepContract : BlockContract
{
    protected override Type Block => typeof(ScopeFreezeStep);

    protected override BlockDescriptor Descriptor => ScopingSteps.Freeze;

    protected override string ExpectedHash => "sha256:8f8134030f9d583280dce7cd7aa211a23cdaba2aa146319f0743605bb927a1ab";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ScopeFreezeStep(new InMemoryRunArea(), new InMemoryPackets()).Run(ScopingStepContexts.Of("step.scope.freeze", []), cancellationToken);
}
