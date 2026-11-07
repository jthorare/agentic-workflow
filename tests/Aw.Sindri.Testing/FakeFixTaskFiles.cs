using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Testing;

/// <summary>The fix-task files in memory: head to open items, ordered by item id. Held to the same rules as the files on disk (an item is listed once, an empty head is gone).</summary>
public sealed class FakeFixTaskFiles : IFixTaskFiles
{
    private readonly Dictionary<string, SortedDictionary<string, int>> _heads = new(StringComparer.Ordinal);

    /// <summary>Gets the open items of each head, by item id.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Heads => _heads.ToDictionary(static h => h.Key, static h => (IReadOnlyDictionary<string, int>)h.Value, StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<Result<bool>> Add(string head, ItemId item, int pr, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(head);
        if (!_heads.TryGetValue(head, out var tasks))
        {
            tasks = new SortedDictionary<string, int>(StringComparer.Ordinal);
            _heads[head] = tasks;
        }

        var added = tasks.TryAdd(item.Value, pr);
        return ValueTask.FromResult(Results.Success(added));
    }

    /// <inheritdoc />
    public ValueTask<Result<bool>> Remove(ItemId item, CancellationToken cancellationToken)
    {
        var removed = false;
        foreach (var head in _heads.Where(h => h.Value.ContainsKey(item.Value)).Select(static h => h.Key).ToList())
        {
            removed |= _heads[head].Remove(item.Value);
            if (_heads[head].Count == 0)
            {
                _ = _heads.Remove(head);
            }
        }

        return ValueTask.FromResult(Results.Success(removed));
    }
}
