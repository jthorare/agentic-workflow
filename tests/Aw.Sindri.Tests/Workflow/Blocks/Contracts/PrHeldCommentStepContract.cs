using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Pr;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (PR babysitter, PB-B): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class PrHeldCommentStepContract : BlockContract
{
    protected override Type Block => typeof(PrHeldCommentStep);

    protected override BlockDescriptor Descriptor => PrSteps.HeldComment;

    protected override string ExpectedHash => "sha256:6f9d10a0df09d786d3be1db3cecd73f2d41284b476e44dd38c7132c1fcae70c5";   // repin: the descriptor now reads fact:prState

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrHeldCommentStep(new Aw.Sindri.Testing.Workflow.InMemoryPrCommentMarks()).Run(BuildShipContexts.Of("step.pr.held-comment", []), cancellationToken);
}
