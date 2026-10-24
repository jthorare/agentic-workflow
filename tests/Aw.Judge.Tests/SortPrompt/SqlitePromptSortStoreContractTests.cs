using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.PromptSort;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.SortPrompt;

// The axes and runs reference the decisions table, so Create records decision 'sort-1' in the database first.
[Lane(TestLane.Integration)]
public sealed class SqlitePromptSortStoreContractTests : PromptSortStoreContract, IDisposable
{
    private readonly TempStateDirectory _state = new();

    protected override async ValueTask<IPromptSortStore> Create(CancellationToken cancellationToken)
    {
        var recorded = await _state.DecisionLog().Record(A.Decision().WithId(Decision.Value).Build(), cancellationToken);
        Assert.True(recorded.IsSuccess);
        return new SqlitePromptSortStore(_state.Database());
    }

    protected override ValueTask<IPromptSortStore> CreateUnavailable(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IPromptSortStore>(new SqlitePromptSortStore(_state.UnopenableDatabase()));

    public void Dispose() => _state.Dispose();
}
