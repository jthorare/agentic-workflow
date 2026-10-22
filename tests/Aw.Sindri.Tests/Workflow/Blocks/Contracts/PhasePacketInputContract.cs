using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Input;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Blocks.Input;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The real 'input.phase-packet': it hashes (as its descriptor-only form did), registers once, resolves as a Step input and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class PhasePacketInputContract : BlockContract
{
    protected override Type Block => typeof(PhasePacketInput);

    protected override BlockDescriptor Descriptor => PrInputs.PhasePacket;

    protected override string ExpectedHash => "sha256:e9bda21ca9a0fd38048269de944e1e4e27974cd499f018c7823a5a49bc1a7104";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PhasePacketInput(new Aw.Sindri.Testing.Scoping.InMemoryBriefFiles(), new Aw.Sindri.Testing.Scoping.InMemoryRepoCheckouts(), new ContextInputFixtures.MemoryFacts()).Fill(
            new StepInputContext(BlockId.From(PhasePacketInput.Id), BlockId.From("step.pr.ci-fix"), new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>()),
            cancellationToken);
}
