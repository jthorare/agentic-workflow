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

    protected override string ExpectedHash => "sha256:053eda81f6cc3664609632efc020b5a8aeb49c8fc37e10af00124b43ca4f4a5b";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ScopeFreezeStep(new InMemoryRunArea(), new InMemoryPackets()).Run(ScopingStepContexts.Of("step.scope.freeze", []), cancellationToken);
}
