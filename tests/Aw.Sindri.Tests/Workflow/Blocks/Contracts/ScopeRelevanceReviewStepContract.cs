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

    protected override string ExpectedHash => "sha256:856fe1b54a6dfa12b4d72541d74f7f08c065350a2c691180004959b13d3d0a88";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
