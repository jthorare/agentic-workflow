using Aw.Judge.Core.Ports;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Briefs;

[Lane(TestLane.Unit)]
public sealed class InMemoryBriefStoreContractTests : BriefStoreContract
{
    protected override ValueTask<IBriefStore> Create(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IBriefStore>(new InMemoryBriefStore());

    protected override ValueTask<IBriefStore> CreateUnavailable(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IBriefStore>(new InMemoryBriefStore { Unavailable = true });
}
