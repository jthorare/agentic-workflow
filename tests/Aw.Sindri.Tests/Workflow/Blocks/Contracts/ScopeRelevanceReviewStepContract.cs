using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>An agent Step (descriptor only, no block runtime): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeRelevanceReviewStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => RelevanceSteps.Review;

    protected override string ExpectedHash => "sha256:a45ee3fe2e893f60ff1c97147ba83d7687a7dd87105168440985961401677967";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
