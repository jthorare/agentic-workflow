using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Testing;

/// <summary>An <see cref="IFixTaskFiles"/> that keeps its tasks in memory and remembers every removal asked of it.</summary>
public sealed class FakeFixTaskFiles : IFixTaskFiles
{
    private readonly List<(string Head, ItemId Item, int Pr)> _tasks = [];
    private readonly List<ItemId> _removed = [];

    /// <summary>The open tasks.</summary>
    public IReadOnlyList<(string Head, ItemId Item, int Pr)> Tasks => _tasks;

    /// <summary>The open tasks by head, then item id to PR number; a head with no open task is absent.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Heads =>
        _tasks.GroupBy(static t => t.Head, StringComparer.Ordinal).ToDictionary(static g => g.Key, static g => (IReadOnlyDictionary<string, int>)g.ToDictionary(static t => t.Item.Value, static t => t.Pr, StringComparer.Ordinal), StringComparer.Ordinal);

    /// <summary>The items passed to Remove, in order, whether or not a task held them.</summary>
    public IReadOnlyList<ItemId> Removed => _removed;

    /// <inheritdoc/>
    public ValueTask<Result<bool>> Add(string head, ItemId item, int pr, CancellationToken cancellationToken)
    {
        if (_tasks.Exists(t => t.Item == item))
        {
            return ValueTask.FromResult<Result<bool>>(false);
        }

        _tasks.Add((head, item, pr));
        return ValueTask.FromResult<Result<bool>>(true);
    }

    /// <inheritdoc/>
    public ValueTask<Result<bool>> Remove(ItemId item, CancellationToken cancellationToken)
    {
        _removed.Add(item);
        return ValueTask.FromResult<Result<bool>>(_tasks.RemoveAll(t => t.Item == item) > 0);
    }
}
