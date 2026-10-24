using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.PromptSort;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.SortPrompt;

// Red until 6.1: the stub adapter has no database yet. 6.1 opens a JudgeDatabase over the temp state directory
// (TempStateDirectory.Database()), records decision 'sort-1' in it and passes both to the store.
[Lane(TestLane.Integration)]
public sealed class SqlitePromptSortStoreContractTests : PromptSortStoreContract
{
    protected override ValueTask<IPromptSortStore> Create(CancellationToken cancellationToken)
    {
        Assert.Skip(RedUntil.Sorter);
        return ValueTask.FromResult<IPromptSortStore>(new SqlitePromptSortStore());
    }

    protected override ValueTask<IPromptSortStore> CreateUnavailable(CancellationToken cancellationToken)
    {
        Assert.Skip(RedUntil.Sorter);
        return ValueTask.FromResult<IPromptSortStore>(new SqlitePromptSortStore());
    }
}
