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

    protected override string ExpectedHash => "sha256:20c2d994c8eccd61fc13019ac160ee6d03bf6f10e84370e766179b91ec5621c7";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
