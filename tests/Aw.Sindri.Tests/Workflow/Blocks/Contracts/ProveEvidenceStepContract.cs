using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Prove;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The evidence Step of the line's prove station on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class ProveEvidenceStepContract : BlockContract
{
    protected override Type Block => typeof(ProveEvidenceStep);

    protected override BlockDescriptor Descriptor => ProveSteps.Evidence;

    protected override string ExpectedHash => "sha256:5f7170da2696e94e8d468c87b60f16c97dcaae5a4d9051548171976460eecab7";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ProveEvidenceStep(new InMemoryBriefFiles(), new InMemoryRunArea(), new InMemoryPackets(), new InMemoryRepoCheckouts()).Run(BuildShipContexts.Of("step.prove.evidence", []), cancellationToken);
}
