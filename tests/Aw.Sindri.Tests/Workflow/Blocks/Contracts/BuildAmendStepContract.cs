using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Build;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The amend Step of the line's build station on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class BuildAmendStepContract : BlockContract
{
    protected override Type Block => typeof(BuildAmendStep);

    protected override BlockDescriptor Descriptor => BuildSteps.Amend;

    protected override string ExpectedHash => "sha256:c9dc4c111e5fc4bf26b87f0e240fafbbd3958d49d00e8c525bcd156158693358";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new BuildAmendStep(new InMemoryPackets(), new InMemoryPacketDirectory(), new InMemoryRunArea(), new FakeTimeProvider()).Run(BuildShipContexts.Of("step.build.amend", []), cancellationToken);
}
