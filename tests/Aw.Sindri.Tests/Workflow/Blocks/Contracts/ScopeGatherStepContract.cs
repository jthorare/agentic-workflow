using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (scoping station, BS-1): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeGatherStepContract : BlockContract
{
    protected override Type Block => typeof(ScopeGatherStep);

    protected override BlockDescriptor Descriptor => ScopingSteps.Gather;

    protected override string ExpectedHash => "sha256:22e683bde84acd39cacef907917b65075d739c9d82cbe4a84eec782be7c770ad";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ScopeGatherStep().Run(ScopingStepContexts.Of("step.scope.gather", []), cancellationToken);
}
