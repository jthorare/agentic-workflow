using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class DeskChooseStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ChoiceSteps.DeskChoose;

    protected override string ExpectedHash => "sha256:d7f8146bcf35afb979ff45d6e8b122a2f7cf9c6327b23e93caf68a49b4940be3";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
