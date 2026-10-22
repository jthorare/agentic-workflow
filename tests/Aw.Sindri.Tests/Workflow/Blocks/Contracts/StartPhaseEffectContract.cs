using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class StartPhaseEffectContract : BlockContract
{
    protected override Type Block => typeof(StartPhaseEffect);

    protected override BlockDescriptor Descriptor => StartPhaseEffect.Descriptor;

    protected override string ExpectedHash => "sha256:495c793b4e745bcf60b92a40ecc344e16b29a399267cf15347b01eabcca8991f";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken)
    {
        var facts = new InMemoryFactStore();
        _ = await new StartPhaseEffect(new FakePhaseStarts(), facts, facts, new FakeTimeProvider()).Dispatch(ShippedComposition.Effect("effect.line.start-phase"), cancellationToken);
    }
}
