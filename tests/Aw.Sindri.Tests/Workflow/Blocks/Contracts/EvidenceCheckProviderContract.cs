using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Features.Blocks.Evidence;
using Aw.Sindri.Testing.BugFix;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime evidence provider: it hashes, kinds, registers once, resolves under IEvidenceProvider and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class EvidenceCheckProviderContract : BlockContract
{
    protected override Type Block => typeof(EvidenceCheckProvider);

    protected override BlockDescriptor Descriptor => EvidenceProviders.Check;

    protected override string ExpectedHash => "sha256:5e8052c5aaf078f6a487a5822fa63d9821a011fa480205fe5bbc8e9f07092d62";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new EvidenceCheckProvider(new InMemoryBugFixState(), new InMemoryRunArea())
            .Collect(new EvidenceContext(BlockId.From("evidence.check"), "check", EvidencePoint.Head, new Dictionary<FactKey, JsonElement>()), cancellationToken);
}
