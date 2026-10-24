using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class PrMergeEffectContract : BlockContract
{
    protected override Type Block => typeof(PrMergeEffect);

    protected override BlockDescriptor Descriptor => PrEffects.Merge;

    protected override string ExpectedHash => "sha256:806168ba9b2c639a20b22db8af9dccca739a29b1044d31ab9454fc064a0c19f7";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrMergeEffect(new RecordingPulls(), new InMemoryFactStore(), new InMemoryProfiles(), new MergeRecord(new InMemoryRunArea(), TimeProvider.System))
            .Dispatch(ShippedComposition.Effect("effect.pr.merge"), cancellationToken);
}
