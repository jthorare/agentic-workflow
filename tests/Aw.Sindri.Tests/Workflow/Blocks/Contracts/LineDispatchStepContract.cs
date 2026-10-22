using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The line's dispatch Step on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class LineDispatchStepContract : BlockContract
{
    protected override Type Block => typeof(LineDispatchStep);

    protected override BlockDescriptor Descriptor => LineSteps.Dispatch;

    protected override string ExpectedHash => "sha256:dc8bf29d7a7a745a6ddc17c4a05c5b265a09a206c82f43eb026f322dc3a2bcf0";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new LineDispatchStep(new InMemoryProfiles()).Run(BuildShipContexts.Of("step.line.dispatch", new Dictionary<string, string>(StringComparer.Ordinal) { ["phaseBoard"] = "{\"phases\":[]}", ["phaseEvent"] = "{}", ["boardEdit"] = "{}" }), cancellationToken);
}
