using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Pr;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (PR babysitter, PB-B): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class PrNextCommentStepContract : BlockContract
{
    protected override Type Block => typeof(PrNextCommentStep);

    protected override BlockDescriptor Descriptor => PrSteps.NextComment;

    protected override string ExpectedHash => "sha256:51abfe654381a000386c4c144347fe3713ac9e55c6039609cb84cf6028244aed";   // repin: the descriptor now reads fact:prState

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrNextCommentStep().Run(BuildShipContexts.Of("step.pr.next-comment", []), cancellationToken);
}
