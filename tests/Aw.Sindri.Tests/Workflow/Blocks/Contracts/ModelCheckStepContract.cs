using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.ModelCheck;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.ModelCheck;
using Aw.Sindri.Testing.Checks;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.BuildShip;
using Aw.Sindri.Tests.Workflow.ModelCheck;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The model-check Step on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class ModelCheckStepContract : BlockContract
{
    protected override Type Block => typeof(ModelCheckStep);

    protected override BlockDescriptor Descriptor => ModelCheckSteps.Check;

    protected override string ExpectedHash => "sha256:fbb8e322dc7f3fc4718c73237190c93eaf6656fad64b99e060df53f4519b26a4";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken)
    {
        var rig = new StepRig();
        var setup = new ModelCheckSetup("/runner/tlc-check.sh", "/usr/bin", "/home/u", "/home/u/.state", "/runner/tlc.sh", "2g", "/tmp/m");
        _ = await new ModelCheckStep(new FakeSpecFolders(), rig.Changes, rig.Runner, new IdentityScrubber(), setup).Run(BuildShipContexts.Of("step.model.check", []), cancellationToken);
    }
}
