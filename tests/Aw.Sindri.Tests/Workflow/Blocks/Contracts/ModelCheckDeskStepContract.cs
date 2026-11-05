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

    protected override string ExpectedHash => "sha256:3b0065f2b8c1b35e5d205e2dfe3b34c02c082d90a6b56e39d8700b480b558db6";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
