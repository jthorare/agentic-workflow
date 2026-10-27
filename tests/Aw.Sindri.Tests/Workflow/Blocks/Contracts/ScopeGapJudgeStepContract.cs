using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Judge;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (gap search, issue 966): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeGapJudgeStepContract : BlockContract
{
    protected override Type Block => typeof(GapJudgeStep);

    protected override BlockDescriptor Descriptor => GapSearchSteps.GapJudge;

    protected override string ExpectedHash => "sha256:44be6fbace363693cbaf1f75b84c1a0aeea03134fcf29e17796b42159693707b";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new GapJudgeStep(new FakeJudgeChoice(), new PassThroughScrubber()).Run(ScopingStepContexts.Of("step.scope.gap-judge", []), cancellationToken);
}
