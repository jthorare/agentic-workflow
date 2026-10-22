using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class PrCreateStackedEffectContract : BlockContract
{
    protected override Type Block => typeof(PrCreateStackedEffect);

    protected override BlockDescriptor Descriptor => PrCreateStackedEffect.Descriptor;

    protected override string ExpectedHash => "sha256:TODO-paste-from-the-failing-test";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken)
    {
        var facts = new InMemoryFactStore();
        _ = await new PrCreateStackedEffect(new FakePullRequests(["github.com"]), new FakeRunDiff(), facts, facts)
            .Dispatch(ShippedComposition.Effect("effect.pr.create-stacked"), cancellationToken);
    }
}
