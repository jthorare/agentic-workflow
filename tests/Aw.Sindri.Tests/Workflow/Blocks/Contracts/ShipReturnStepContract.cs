using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Ship;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (build and ship stations, BS-3): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class ShipReturnStepContract : BlockContract
{
    protected override Type Block => typeof(ShipReturnStep);

    protected override BlockDescriptor Descriptor => ShipSteps.Return;

    protected override string ExpectedHash => "sha256:b83d017741b130dab48923435dfb5e866215591086c2f10933f89eb59260702d";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ShipReturnStep().Run(BuildShipContexts.Of("step.ship.return", []), cancellationToken);
}
