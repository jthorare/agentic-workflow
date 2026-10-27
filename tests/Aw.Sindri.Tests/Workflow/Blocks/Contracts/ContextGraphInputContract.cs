using System.Text.Json;
using Aw.Context.Testing.Engine;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Blocks.Input;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The real 'input.context.graph': it hashes, registers once, resolves as a Step input and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextGraphInputContract : BlockContract
{
    protected override Type Block => typeof(ContextGraphInput);

    protected override BlockDescriptor Descriptor => ContextGraphInput.Descriptor;

    protected override string ExpectedHash => "sha256:2c17208d3e2e261c19e4986ca31aee485635cd53cabeccec1cf2fd546e8023bf";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ContextGraphInput(FakeContextEngine.Instance, new ContextInputScopes(new ContextInputFixtures.Profiles(ContextInputFixtures.Profile())), ContextInputFixtures.NoFacts(), ShippedComposition.Json).Fill(
            new StepInputContext(BlockId.From(ContextGraphInput.Id), BlockId.From("step.scope.gather"), new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>()),
            cancellationToken);
}
