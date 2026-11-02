using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class InterviewQuestionsStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ScopingSteps.InterviewQuestions;

    protected override string ExpectedHash => "sha256:7f66c41ee6dd95b683d37f9de7d64a089e6a316ee4028636c1dfc1d0a780ad08";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
