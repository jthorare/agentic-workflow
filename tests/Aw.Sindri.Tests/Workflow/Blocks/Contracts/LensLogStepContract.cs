using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.LensLoops;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Lens;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (lens and review loops, BS-4): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class LensLogStepContract : BlockContract
{
    protected override Type Block => typeof(LensLogStep);

    protected override BlockDescriptor Descriptor => LensRoundSteps.Log;

    protected override string ExpectedHash => "sha256:29b0a0f290bc739ccd510df2775dd46e9a211c337ad02ba07c5481d9338b4f66";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new LensLogStep().Run(LensStepContexts.Of("step.lens.log", []), cancellationToken);
}
