using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class PrRecordEffectContract : BlockContract
{
    protected override Type Block => typeof(PrRecordEffect);

    protected override BlockDescriptor Descriptor => PrEffects.Record;

    protected override string ExpectedHash => "sha256:6cccfc9b02bd6d8d2c3fc14270043193dbba32f1016ad049bc67593f9ed91643";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrRecordEffect(new InMemoryRunArea(), new InMemoryFactStore(), TimeProvider.System)
            .Dispatch(ShippedComposition.Effect("effect.pr.record"), cancellationToken);
}
