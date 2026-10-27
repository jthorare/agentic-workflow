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
public sealed class ScopeIntakeStepContract : BlockContract
{
    protected override Type Block => typeof(ScopeIntakeStep);

    protected override BlockDescriptor Descriptor => ScopingSteps.Intake;

    protected override string ExpectedHash => "sha256:46454a0eea1c31482d12da9f804f0b8b97df0812eea69be60407d2e9c49989f2";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ScopeIntakeStep().Run(ScopingStepContexts.Of("step.scope.intake", []), cancellationToken);
}
