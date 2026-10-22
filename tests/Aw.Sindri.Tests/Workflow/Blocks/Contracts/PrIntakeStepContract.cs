using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block whose runtime is registered once: it hashes, kinds, registers once and resolves nothing here; its behavior is held by its own tests.</summary>
[Lane(TestLane.Unit)]
public sealed class PrIntakeStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrSteps.Intake;

    protected override string ExpectedHash => "sha256:89e24d042519a97386cbd1cbd3f1a149c62ab185aad323af77a07d278a7742eb";

    protected override bool HasRuntime => true;

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
