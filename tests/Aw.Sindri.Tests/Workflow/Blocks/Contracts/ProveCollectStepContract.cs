using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Prove;
using Aw.Sindri.Testing.Prove;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The collect Step of the line's prove station, a block runtime (it hashes the files the evidence children name): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ProveCollectStepContract : BlockContract
{
    protected override Type Block => typeof(ProveCollectStep);

    protected override BlockDescriptor Descriptor => ProveSteps.Collect;

    protected override string ExpectedHash => "sha256:c67b2ae514c786d44d3c2a5d2edab74fcdae626dd7a7bc81710237e3cfa8b073";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ProveCollectStep(new InMemoryArtifactFiles(), new FakeTimeProvider()).Run(BuildShipContexts.Of("step.prove.collect", []), cancellationToken);
}
