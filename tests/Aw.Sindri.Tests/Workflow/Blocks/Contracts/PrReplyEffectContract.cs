using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Checks;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class PrReplyEffectContract : BlockContract
{
    protected override Type Block => typeof(PrReplyEffect);

    protected override BlockDescriptor Descriptor => PrEffects.Reply;

    protected override string ExpectedHash => "sha256:ff2ea187d268bff8e2c32affa1061c3e2b79f9dc9b4dd34e494dcdb97ab47dbe";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrReplyEffect(new RecordingPulls(), new InMemoryFactStore(), new InMemoryPrCommentMarks(), TimeProvider.System, new IdentityScrubber(), new MergeRecord(new InMemoryRunArea(), TimeProvider.System), new Aw.Sindri.Tests.Workflow.Pr.RecordingFixItemCloser())
            .Dispatch(ShippedComposition.Effect("effect.pr.reply"), cancellationToken);
}
