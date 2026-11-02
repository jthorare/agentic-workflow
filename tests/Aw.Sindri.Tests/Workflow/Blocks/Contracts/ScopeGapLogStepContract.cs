using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (gap search, issue 966): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeGapLogStepContract : BlockContract
{
    protected override Type Block => typeof(GapLogStep);

    protected override BlockDescriptor Descriptor => GapSearchSteps.GapLog;

    protected override string ExpectedHash => "sha256:66af080e2131f37f8185d40e9e09951f0a3175c99440087cd1d15eed8105c080";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new GapLogStep().Run(ScopingStepContexts.Of("step.scope.gap-log", []), cancellationToken);
}
