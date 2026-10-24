using Aw.Judge.Core.Ports;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.SortPrompt;

[Lane(TestLane.Unit)]
public sealed class InMemorySortSessionStoreContractTests : SortSessionStoreContract
{
    protected override ValueTask<ISortSessionStore> Create(CancellationToken cancellationToken) =>
        ValueTask.FromResult<ISortSessionStore>(new InMemorySortSessionStore());

    protected override ValueTask<ISortSessionStore> CreateUnavailable(CancellationToken cancellationToken) =>
        ValueTask.FromResult<ISortSessionStore>(new InMemorySortSessionStore { Unavailable = true });
}
