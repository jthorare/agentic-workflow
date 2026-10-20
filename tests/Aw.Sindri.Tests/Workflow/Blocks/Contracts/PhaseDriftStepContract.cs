using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Build;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (build and ship stations, BS-3): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class PhaseDriftStepContract : BlockContract
{
    protected override Type Block => typeof(PhaseDriftStep);

    protected override BlockDescriptor Descriptor => PhaseSteps.Drift;

    protected override string ExpectedHash => "sha256:b5605417cc265fa86f6f93042dfb1218434307d27f3c0f7379a549733b0b477e";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PhaseDriftStep(new FakeRunDiff()).Run(BuildShipContexts.Of("step.phase.drift", []), cancellationToken);
}
