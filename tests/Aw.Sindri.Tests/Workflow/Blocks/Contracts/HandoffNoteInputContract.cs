using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Input;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Blocks.Input;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The real 'input.handoff-note': it hashes (as its descriptor-only form did), registers once, resolves as a Step input and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class HandoffNoteInputContract : BlockContract
{
    protected override Type Block => typeof(HandoffNoteInput);

    protected override BlockDescriptor Descriptor => PrInputs.HandoffNote;

    protected override string ExpectedHash => "sha256:b729b5df3a097a2560e0fd12f9ddef904eea572a68a0ee3fe211819f3b0aec9b";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new HandoffNoteInput(new ContextInputFixtures.MemoryFacts()).Fill(
            new StepInputContext(BlockId.From(HandoffNoteInput.Id), BlockId.From("step.pr.ci-fix"), new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>()),
            cancellationToken);
}
