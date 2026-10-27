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
public sealed class ScopeChallengeLogStepContract : BlockContract
{
    protected override Type Block => typeof(ChallengeLogStep);

    protected override BlockDescriptor Descriptor => ScopingSteps.ChallengeLog;

    protected override string ExpectedHash => "sha256:b12a5e72b972823609c3ed3d46f0f2a5648cde563568f7a9d31193434d9327eb";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ChallengeLogStep().Run(ScopingStepContexts.Of("step.scope.challenge-log", []), cancellationToken);
}
