using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeDraftStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ScopingSteps.Draft;

    protected override string ExpectedHash => "sha256:0a88de2a7ee4ebdc321460e1f0d84b4b6a7a7ad8a0229e2ac50bc9d081147df9";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
