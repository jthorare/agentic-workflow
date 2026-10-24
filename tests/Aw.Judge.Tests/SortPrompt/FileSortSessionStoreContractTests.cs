using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.PromptSort;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.SortPrompt;

// Red until 6.1: the stub adapter has no directory yet. 6.1 passes it the temp state directory's judge/sessions (and, for
// the unwritable case, a path under a regular file), and adds the tests only a file can have: keys it does not know
// survive a write, the replace is atomic (no .tmp left), and a file a TS sorter left behind reads as is.
[Lane(TestLane.Integration)]
public sealed class FileSortSessionStoreContractTests : SortSessionStoreContract
{
    protected override ValueTask<ISortSessionStore> Create(CancellationToken cancellationToken)
    {
        Assert.Skip(RedUntil.Sorter);
        return ValueTask.FromResult<ISortSessionStore>(new FileSortSessionStore());
    }

    protected override ValueTask<ISortSessionStore> CreateUnavailable(CancellationToken cancellationToken)
    {
        Assert.Skip(RedUntil.Sorter);
        return ValueTask.FromResult<ISortSessionStore>(new FileSortSessionStore());
    }
}
