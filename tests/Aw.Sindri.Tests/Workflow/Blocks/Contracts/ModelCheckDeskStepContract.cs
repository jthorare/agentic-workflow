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

    protected override string ExpectedHash => "sha256:73c49e5667ce192a6c75454838a7240e3e129d77ea0af3b98670095f4fb2a441";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
