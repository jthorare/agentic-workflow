using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class RevisionBoundGateContract : BlockContract
{
    protected override Type Block => typeof(RevisionBoundGate);

    protected override BlockDescriptor Descriptor => RevisionBoundGate.Descriptor;

    protected override string ExpectedHash => "sha256:a12d7104640c59aa648f424df3a403772e2418c2dd3db57466b0d4966646444f";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new RevisionBoundGate().Evaluate(
            new GateContext(RevisionBoundGate.Descriptor.Id, new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>(), Threshold: null),
            cancellationToken);
}
