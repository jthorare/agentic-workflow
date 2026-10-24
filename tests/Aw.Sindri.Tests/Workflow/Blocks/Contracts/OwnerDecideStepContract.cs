using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class OwnerDecideStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => OwnerSteps.Decide;

    protected override string ExpectedHash => "sha256:620a9e89b802990887be0e676adedcecaf6c7630b306aa3c778b955cb98d12c5";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
