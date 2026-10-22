using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class PrSecretScanGateContract : BlockContract
{
    protected override Type Block => typeof(PrSecretScanGate);

    protected override BlockDescriptor Descriptor => PrSecretScanGate.Descriptor;

    protected override string ExpectedHash => "sha256:29947b4a2e53135dc91b191d90fc62b7ff5d4ff203cf3812b28e174984af6192";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrSecretScanGate(new FakeDiffContent(), new RedactingTestScrubber()).Evaluate(
            new GateContext(BlockId.From("gate.pr.secret-scan"), new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>(), Threshold: null),
            cancellationToken);

    private sealed class RedactingTestScrubber : ITextScrubber
    {
        public string Scrub(string text) => text;
    }
}
