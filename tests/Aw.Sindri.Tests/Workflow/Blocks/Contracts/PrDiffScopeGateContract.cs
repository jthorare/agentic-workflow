using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Testing.LensPacks;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class PrDiffScopeGateContract : BlockContract
{
    protected override Type Block => typeof(PrDiffScopeGate);

    protected override BlockDescriptor Descriptor => PrDiffScopeGate.Descriptor;

    protected override string ExpectedHash => "sha256:cb511e0ce1cd41189d97db37b92e31daed878627e984f0b26080a3d7e81c2583";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrDiffScopeGate(new FakeChangedPaths(), new FakeDiffContent()).Evaluate(
            new GateContext(BlockId.From("gate.pr.diff-scope"), new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>(), Threshold: null),
            cancellationToken);
}
