using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Checks;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class PrPushEffectContract : BlockContract
{
    protected override Type Block => typeof(PrPushEffect);

    protected override BlockDescriptor Descriptor => PrEffects.Push;

    protected override string ExpectedHash => "sha256:2939a0d119c4875ad5e9d48dcab5e360f52b298f4ef8c3e11a069b8810a1f970";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrPushEffect(new FakePullRequests([]), new FakeRunDiff(), new InMemoryBranchChanges(), new InMemoryFactStore(), new InMemoryFactStore())
            .Dispatch(ShippedComposition.Effect("effect.pr.push"), cancellationToken);
}
