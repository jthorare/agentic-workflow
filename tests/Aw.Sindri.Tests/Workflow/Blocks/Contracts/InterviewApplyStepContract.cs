using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class InterviewApplyStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ScopingSteps.InterviewApply;

    protected override string ExpectedHash => "sha256:6b0c46e0dc8b016d3413f24b8fef52ab8a67173c5e9f6d7cb3e801d7a099ed9d";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
