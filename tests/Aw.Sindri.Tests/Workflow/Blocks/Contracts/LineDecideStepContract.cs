using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A desk-only block: it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class LineDecideStepContract : DescriptorContract
{

    protected override BlockDescriptor Descriptor => LineSteps.Decide;

    protected override string ExpectedHash => "sha256:3c46794ff68a2c43284541e71bcb0e730e034931129277311d557f45a38adf91";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
