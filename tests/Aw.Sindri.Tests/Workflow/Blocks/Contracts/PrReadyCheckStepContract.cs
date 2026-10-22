using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Pr;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step: it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class PrReadyCheckStepContract : BlockContract
{
    protected override Type Block => typeof(PrReadyCheckStep);

    protected override BlockDescriptor Descriptor => PrSteps.ReadyCheck;

    protected override string ExpectedHash => "sha256:f5a609f75a5a5ada53d92a34be422f0075fdafc476dde64119cd8cb3aa37b531";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrReadyCheckStep(new FakePullRequests(["forge.test"])).Run(BuildShipContexts.Of("step.pr.ready-check", []), cancellationToken);
}
