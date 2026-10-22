using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The line's seed Step on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class LineSeedStepContract : BlockContract
{
    protected override Type Block => typeof(LineSeedStep);

    protected override BlockDescriptor Descriptor => LineSteps.Seed;

    protected override string ExpectedHash => "sha256:57c15de7bfb1b236631e3162f2cb058786ffd54f3b429b6b7dbfbc43b36fd3c6";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new LineSeedStep().Run(BuildShipContexts.Of("step.line.seed", new Dictionary<string, string>(StringComparer.Ordinal) { ["phasePackets"] = "[]" }), cancellationToken);
}
