using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Blocks.Input;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The real 'input.context.retrieve': it hashes, registers once, resolves as a Step input and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextRetrieveInputContract : BlockContract
{
    protected override Type Block => typeof(ContextRetrieveInput);

    protected override BlockDescriptor Descriptor => ContextRetrieveInput.Descriptor;

    protected override string ExpectedHash => "sha256:1f9f3e89aa4fcd85378c1b4a09252633a783b9848fec554206049dbe79a36410";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await ContextRetrieveInputTests.Input(ContextRetrieveInputTests.NewEngine()).Fill(
            new StepInputContext(BlockId.From(ContextRetrieveInput.Id), BlockId.From("step.scope.gather"), new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>()),
            cancellationToken);
}
