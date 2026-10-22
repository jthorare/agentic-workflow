using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Build;
using Aw.Sindri.Testing.Checks;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.BuildShip;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The checks Step of the line's build station on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class BuildChecksStepContract : BlockContract
{
    protected override Type Block => typeof(BuildChecksStep);

    protected override BlockDescriptor Descriptor => BuildSteps.Checks;

    protected override string ExpectedHash => "sha256:0cd3455566045c33e7cb5e8b0d870607ab76a6ba147fd61220e6bff4d72ff891";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken)
    {
        var rig = new StepRig();
        _ = await new BuildChecksStep(rig.Diff, rig.Changes, rig.Files, rig.Runner, new IdentityScrubber()).Run(BuildShipContexts.Of("step.build.checks", []), cancellationToken);
    }
}
