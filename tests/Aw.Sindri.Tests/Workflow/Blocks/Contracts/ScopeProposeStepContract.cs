using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (scoping station, BS-1): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeProposeStepContract : BlockContract
{
    protected override Type Block => typeof(ScopeProposeStep);

    protected override BlockDescriptor Descriptor => ScopingSteps.Propose;

    protected override string ExpectedHash => "sha256:ece43ff6ea5e3886e320d09a3b2edcf1a1e3f949db3f15a3160ae08f76826f9e";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ScopeProposeStep(new InMemoryScopeRunProposals()).Run(ScopingStepContexts.Of("step.scope.propose", []), cancellationToken);
}
