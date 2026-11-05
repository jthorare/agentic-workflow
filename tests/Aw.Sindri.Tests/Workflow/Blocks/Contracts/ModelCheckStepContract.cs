using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.ModelCheck;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.ModelCheck;
using Aw.Sindri.Infrastructure.ModelCheck;
using Aw.Sindri.Testing.Checks;
using Aw.Sindri.Testing.Packets;
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

    protected override string ExpectedHash => "sha256:a889a75856c004e1ff4861923cfd6752e8ed707e0299f4c5ccc9ec56f154c76f";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken)
    {
        var rig = new StepRig();
        var setup = new ModelCheckSetup("/runner/tlc-check.sh", "/usr/bin", "/home/u", "/home/u/.state", "/runner/tlc.sh", "2g", "/tmp/m");
        _ = await new ModelCheckStep(new FakeSpecFolders(), rig.Changes, rig.Runner, new IdentityScrubber(), setup, new InMemoryPacketDirectory(), new DiskScratchDirectories()).Run(BuildShipContexts.Of("step.model.check", []), cancellationToken);
    }
}
