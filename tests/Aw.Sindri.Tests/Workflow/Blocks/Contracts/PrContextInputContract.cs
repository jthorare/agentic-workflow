using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Input;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.LensPacks;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Blocks.Input;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The real 'input.pr.context': it hashes (as its descriptor-only form did), registers once, resolves as a Step input and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class PrContextInputContract : BlockContract
{
    protected override Type Block => typeof(PrContextInput);

    protected override BlockDescriptor Descriptor => PrInputs.Context;

    protected override string ExpectedHash => "sha256:fe06fb61808b54d6b99c831295c419f00f6f9c57bb48a465b137053339645d15";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrContextInput(new FakePullRequests([]), new FakeChangedPaths(), new PassThroughScrubber(), new ContextInputFixtures.MemoryFacts()).Fill(
            new StepInputContext(BlockId.From(PrContextInput.Id), BlockId.From("step.pr.ci-fix"), new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>()),
            cancellationToken);
}
