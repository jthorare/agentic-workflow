using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (scoping station, BS-1): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeEvidenceStepContract : BlockContract
{
    protected override Type Block => typeof(ScopeEvidenceStep);

    protected override BlockDescriptor Descriptor => ScopingSteps.Evidence;

    protected override string ExpectedHash => "sha256:0642b99faa2246448802631c14466a0da270a858a3d82a335210df76fafa1df1";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ScopeEvidenceStep(new InMemoryRunArea(), new InMemoryPackets(), new InMemoryRepoCheckouts(), new FakeRunDiff(), new FakeScopeRunRecords()).Run(ScopingStepContexts.Of("step.scope.evidence", []), cancellationToken);
}
