using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class PrRerunChecksEffectContract : BlockContract
{
    protected override Type Block => typeof(PrRerunChecksEffect);

    protected override BlockDescriptor Descriptor => PrEffects.RerunChecks;

    protected override string ExpectedHash => "sha256:f0bdf81897ba0334bd3a6d51de21642cc43e9982767ac0690812b29ec6c5bf34";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrRerunChecksEffect(new FakePullRequests([]), new InMemoryFactStore())
            .Dispatch(ShippedComposition.Effect("effect.pr.rerun-checks"), cancellationToken);
}
