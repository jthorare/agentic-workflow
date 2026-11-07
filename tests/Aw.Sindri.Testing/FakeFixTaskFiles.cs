using Aw.Kernel;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Testing;

/// <summary>An <see cref="IFixTaskFiles"/> that keeps its tasks in memory and remembers every removal asked of it.</summary>
public sealed class FakeFixTaskFiles : IFixTaskFiles
{
    private readonly List<(string Head, string Item, int Pr)> _tasks = [];
    private readonly List<string> _removed = [];

    /// <summary>The open tasks.</summary>
    public IReadOnlyList<(string Head, string Item, int Pr)> Tasks => _tasks;

    /// <summary>The items passed to Remove, in order, whether or not a task held them.</summary>
    public IReadOnlyList<string> Removed => _removed;

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Add(string head, string item, int pr, CancellationToken cancellationToken)
    {
        if (!_tasks.Exists(t => t.Item == item))
        {
            _tasks.Add((head, item, pr));
        }

        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Remove(string item, CancellationToken cancellationToken)
    {
        _removed.Add(item);
        _ = _tasks.RemoveAll(t => t.Item == item);
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }
}
