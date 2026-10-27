using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (scoping relevance review): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeRelevanceApplyStepContract : BlockContract
{
    protected override Type Block => typeof(RelevanceApplyStep);

    protected override BlockDescriptor Descriptor => RelevanceSteps.Apply;

    protected override string ExpectedHash => "sha256:b54684cd6a8d66aee8d1f18ab1cc28525cec2d7d1efc90f3b427eeba8486f255";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new RelevanceApplyStep().Run(ScopingStepContexts.Of("step.scope.relevance-apply", []), cancellationToken);
}
