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

    protected override string ExpectedHash => "sha256:cf99ec2b9591f96ad8ae5a65ce2a59cf5d02337ea21b581fb372167171986304";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
