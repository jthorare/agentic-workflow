using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Ports;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Labels;

[Lane(TestLane.Unit)]
public sealed class InMemoryEvalStoreContractTests : EvalStoreContract
{
    protected override ValueTask<IEvalStore> Create(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IEvalStore>(new InMemoryEvalStore());

    protected override ValueTask<IEvalStore> CreateUnavailable(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IEvalStore>(new InMemoryEvalStore { Unavailable = true });

    protected override ValueTask SeedDecision(IEvalStore store, DecidedInput input, DateTimeOffset at, SessionId? session, CancellationToken cancellationToken)
    {
        ((InMemoryEvalStore)store).SeedDecision(input, at, session);
        return ValueTask.CompletedTask;
    }
}
