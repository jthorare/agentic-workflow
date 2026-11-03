using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Judge;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Judge;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The decision-tree Step 'step.judge.choose' on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class JudgeChooseStepContract : BlockContract
{
    protected override Type Block => typeof(JudgeChooseStep);

    protected override BlockDescriptor Descriptor => ChoiceSteps.JudgeChoose;

    protected override string ExpectedHash => "sha256:762759ee47f6ab3f4fd9deccaab7e2b1050780070b74861262a57a8cff8589eb";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new JudgeChooseStep(new FakeJudgeChoice(), new PassThroughScrubber()).Run(JudgeChooseStepTests.Context(), cancellationToken);
}
