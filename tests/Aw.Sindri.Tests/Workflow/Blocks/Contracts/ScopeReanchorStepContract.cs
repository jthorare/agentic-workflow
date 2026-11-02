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
public sealed class ScopeReanchorStepContract : BlockContract
{
    protected override Type Block => typeof(ScopeReanchorStep);

    protected override BlockDescriptor Descriptor => ScopingSteps.Reanchor;

    protected override string ExpectedHash => "sha256:1f871ecc02474149c071ef8577b9486b1b1d0408734a283e4fcf009b416b14e7";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ScopeReanchorStep(new InMemoryScopingTextShape([])).Run(ScopingStepContexts.Of("step.scope.reanchor", []), cancellationToken);
}
