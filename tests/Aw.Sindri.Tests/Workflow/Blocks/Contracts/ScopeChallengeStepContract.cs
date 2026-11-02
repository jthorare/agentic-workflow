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

    protected override string ExpectedHash => "sha256:17e22e8d331fef7a34b81d3d8215b3fa1052cc79b1f9a53081585ab65cc1d8a1";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
