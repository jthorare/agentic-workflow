using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Prove;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Worktrees;
using Aw.Sindri.Tests.Workflow.BuildShip;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The before/after Step of the line's prove station on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class ProveBeforeAfterStepContract : BlockContract
{
    protected override Type Block => typeof(ProveBeforeAfterStep);

    protected override BlockDescriptor Descriptor => ProveSteps.BeforeAfter;

    protected override string ExpectedHash => "sha256:262fd4a9557d27a21e413eeffc1365cf76f1b9c8ea6dec3a6caa3095b01c5ce0";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ProveBeforeAfterStep(new InMemoryBriefFiles(), new InMemoryRunArea(), new InMemoryRepoCheckouts(), new FakeWorktrees(), new FakeScriptRuns(), new StepRig("dotnet test").Runner).Run(BuildShipContexts.Of("step.prove.before-after", []), cancellationToken);
}
