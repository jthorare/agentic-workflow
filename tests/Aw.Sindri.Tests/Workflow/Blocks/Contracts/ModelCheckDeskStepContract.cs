using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The model desk Step, descriptor-only (a human answers it): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ModelCheckDeskStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ModelCheckSteps.Desk;

    protected override string ExpectedHash => "sha256:0000000000000000000000000000000000000000000000000000000000000000";   // pinned by the first run: the failing assertion prints the hash

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
