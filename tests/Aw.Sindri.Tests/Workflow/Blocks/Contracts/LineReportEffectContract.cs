using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class LineReportEffectContract : BlockContract
{
    protected override Type Block => typeof(LineReportEffect);

    protected override BlockDescriptor Descriptor => LineEffects.Report;

    protected override string ExpectedHash => "sha256:7e5951377422c5c199863a5b9669d8ba5895100a52a87f876bf1fd56d174d63b";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken)
    {
        var facts = new InMemoryFactStore();
        _ = await new LineReportEffect(new InMemoryRunArea(), facts, facts, TimeProvider.System)
            .Dispatch(ShippedComposition.Effect("effect.line.report"), cancellationToken);
    }
}
