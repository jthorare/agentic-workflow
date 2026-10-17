using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class JudgeChooseStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ChoiceSteps.JudgeChoose;

    protected override string ExpectedHash => "sha256:270ab4f63fe46487ca948e4f254eb80cb50f028acb1bf70419d492db359851a2";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
