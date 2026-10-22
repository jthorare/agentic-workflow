using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Prove;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The re-proof child's Step on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class ProveRerunScriptsStepContract : BlockContract
{
    protected override Type Block => typeof(ProveRerunScriptsStep);

    protected override BlockDescriptor Descriptor => RecheckSteps.RerunScripts;

    protected override string ExpectedHash => "sha256:2e0e9a8b887862de2e9374048a01a2c849837c05999ce9db5bcf51a2bd13d732";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ProveRerunScriptsStep(new FakeScriptRuns(), new InMemoryRunArea()).Run(BuildShipContexts.Of("step.prove.rerun-scripts", []), cancellationToken);
}
