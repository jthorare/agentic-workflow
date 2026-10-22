using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Build;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.BuildShip;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The red-check Step of the line's build station on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class BuildRedCheckStepContract : BlockContract
{
    protected override Type Block => typeof(BuildRedCheckStep);

    protected override BlockDescriptor Descriptor => BuildSteps.RedCheck;

    protected override string ExpectedHash => "sha256:f36bf0694daab113aab40ac73dc27a6f94a5d21a483d62dd160e11bbac22fc17";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken)
    {
        var rig = new StepRig();
        _ = await new BuildRedCheckStep(rig.Diff, rig.Runner).Run(BuildShipContexts.Of("step.build.red-check", []), cancellationToken);
    }
}
