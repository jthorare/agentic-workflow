using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class PrAutofixCapGateContract : BlockContract
{
    protected override Type Block => typeof(PrAutofixCapGate);

    protected override BlockDescriptor Descriptor => PrAutofixCapGate.Descriptor;

    protected override string ExpectedHash => "sha256:2b9014eec74d63507b4de158e4bbde35aaedc6e6f1858594ddc07bbe41c19c84";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrAutofixCapGate().Evaluate(
            new GateContext(PrAutofixCapGate.Descriptor.Id, new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>(), Threshold: null),
            cancellationToken);
}
