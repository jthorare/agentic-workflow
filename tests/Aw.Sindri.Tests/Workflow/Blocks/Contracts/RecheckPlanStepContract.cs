using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Prove;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The re-proof child's Step on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class RecheckPlanStepContract : BlockContract
{
    protected override Type Block => typeof(RecheckPlanStep);

    protected override BlockDescriptor Descriptor => RecheckSteps.Plan;

    protected override string ExpectedHash => "sha256:d89745985e6c9ccf44d99796258359e7e680af9e3f1a5c82bae4aa36a6c73c10";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new RecheckPlanStep(new InMemoryBriefFiles(), new InMemoryRunArea()).Run(BuildShipContexts.Of("step.recheck.plan", []), cancellationToken);
}
