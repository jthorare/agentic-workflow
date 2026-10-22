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
public sealed class RecheckLogStepContract : BlockContract
{
    protected override Type Block => typeof(RecheckLogStep);

    protected override BlockDescriptor Descriptor => RecheckSteps.Log;

    protected override string ExpectedHash => "sha256:f3598a3be81c94e1a07d1e309721d66e8831f60f824bab462ec06ee9560d86e8";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new RecheckLogStep(new InMemoryRunArea()).Run(BuildShipContexts.Of("step.recheck.log", []), cancellationToken);
}
