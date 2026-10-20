using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Build;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The prepare Step of the line's build station (a block runtime: it checks the workspace and seeds the build's facts): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class BuildPrepareStepContract : BlockContract
{
    protected override Type Block => typeof(BuildPrepareStep);

    protected override BlockDescriptor Descriptor => BuildSteps.Prepare;

    protected override string ExpectedHash => "sha256:f09beb073bf7e6aa136b0c18debab7a648fe7c3352c2f19ea660fadfd332d06d";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new BuildPrepareStep(new FakeRunDiff()).Run(BuildShipContexts.Of("step.build.prepare", []), cancellationToken);
}
