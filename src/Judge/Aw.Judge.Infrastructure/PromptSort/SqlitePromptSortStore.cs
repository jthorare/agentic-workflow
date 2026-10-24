using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.PromptSort;
using Aw.Kernel;

namespace Aw.Judge.Infrastructure.PromptSort;

/// <summary>IPromptSortStore over the 'prompt_sort_axes' and 'prompt_sort_runs' tables (migration 0003). Cutover unit 6.1 builds it.</summary>
internal sealed class SqlitePromptSortStore : IPromptSortStore
{
    /// <inheritdoc />
    public ValueTask<Result<Unit>> Save(PromptSortRecord record, CancellationToken cancellationToken)
        => throw new NotSupportedException("SqlitePromptSortStore: cutover unit 6.1");

    /// <inheritdoc />
    public ValueTask<Result<PromptSortRecord>> Find(DecisionId decision, CancellationToken cancellationToken)
        => throw new NotSupportedException("SqlitePromptSortStore: cutover unit 6.1");
}
