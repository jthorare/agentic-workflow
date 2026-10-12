using Aw.Judge.Core.Ports;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Decisions;

[Lane(TestLane.Unit)]
public sealed class InMemoryDecisionLogContractTests : DecisionLogContract
{
    protected override ValueTask<IDecisionLog> Create(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IDecisionLog>(new InMemoryDecisionLog());

    protected override ValueTask<IDecisionLog> CreateUnavailable(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IDecisionLog>(new InMemoryDecisionLog { Unavailable = true });
}
