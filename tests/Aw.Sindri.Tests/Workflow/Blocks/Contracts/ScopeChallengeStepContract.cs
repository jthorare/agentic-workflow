using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeChallengeStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ScopingSteps.Challenge;

    protected override string ExpectedHash => "sha256:bdfa0c4205c9ec57ef61702c6084dccdee3081ea1f6783feb220e5b8e311ce0f";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
