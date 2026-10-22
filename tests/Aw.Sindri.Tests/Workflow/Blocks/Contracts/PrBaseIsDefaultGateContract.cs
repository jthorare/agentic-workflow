using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The PR babysitter's base gate on the block runtime: it hashes (the descriptor is unchanged), kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class PrBaseIsDefaultGateContract : BlockContract
{
    protected override Type Block => typeof(PrBaseIsDefaultGate);

    protected override BlockDescriptor Descriptor => PrBaseIsDefaultGate.Descriptor;

    protected override string ExpectedHash => "sha256:f2ca5e4d3f12a9b90f750f5cfc110213f1ae932bf7da2914e61922d343d543b2";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrBaseIsDefaultGate(new FakeDefaultBranch()).Evaluate(
            new GateContext(BlockId.From("gate.pr.base-is-default"), new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>(), Threshold: null),
            cancellationToken);
}
