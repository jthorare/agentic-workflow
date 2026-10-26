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

    protected override string ExpectedHash => "sha256:0399777e021284ff84db274fc37482f347da4f2b0f22197024c09fa6173da8c8";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ScopeEvidenceStep(new InMemoryRunArea(), new InMemoryPackets(), new InMemoryRepoCheckouts(), new FakeRunDiff(), new FakeScopeRunRecords()).Run(ScopingStepContexts.Of("step.scope.evidence", []), cancellationToken);
}
