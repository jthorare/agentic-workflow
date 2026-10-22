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

    protected override string ExpectedHash => "sha256:0a9099aa941cdd0b33a27df855c0bc7e1d10122e65e6cef067a541717b6e6bb7";   // repin: the descriptor now reads fact:prState

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrHeldCommentStep().Run(BuildShipContexts.Of("step.pr.held-comment", []), cancellationToken);
}
