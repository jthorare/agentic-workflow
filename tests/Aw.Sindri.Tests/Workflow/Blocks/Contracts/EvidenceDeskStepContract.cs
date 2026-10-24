using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class EvidenceDeskStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => EvidenceSteps.Desk;

    protected override string ExpectedHash => "sha256:060a0b89118c9cfa76196e8066a4bbdcea96d2dccb1d1bb50c255ce4b4fffc39";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
