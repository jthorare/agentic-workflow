using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Docs;
using Aw.Sindri.Testing.LensPacks;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Worktrees;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (BS-11): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class DocsScopeStepContract : BlockContract
{
    protected override Type Block => typeof(DocsScopeStep);

    protected override BlockDescriptor Descriptor => DocsSteps.Scope;

    protected override string ExpectedHash => "sha256:588b7dde7f1d787a475280c94f1e18e5e52086c8a7cca1ebc3466a251ab81cc9";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new DocsScopeStep(new InMemoryRepoCheckouts(), new FakeWorktrees(), new FakeChangedPaths(), new FakeTrackedFiles(), new InMemoryBriefFiles()).Run(BuildShipContexts.Of("step.docs.scope", []), cancellationToken);
}
