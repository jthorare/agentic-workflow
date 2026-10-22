using Aw.Kernel;
using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Testing.Forge;

/// <summary>An in-memory <see cref="IWatchedPulls"/>: a test says which pull requests an instance waits on with <see cref="Watch"/> and stops it with <see cref="Unwatch"/>. Held to <see cref="WatchedPullsContract"/>.</summary>
public sealed class FakeWatchedPulls : IWatchedPulls
{
    private readonly List<WatchedPull> _pulls = [];

    /// <summary>Gets or sets the error every listing answers, or null for none.</summary>
    public Error? Failure { get; set; }

    /// <summary>An instance of <paramref name="item"/> starts to wait on the pull request at <paramref name="link"/>.</summary>
    /// <param name="link">The pull request's link.</param>
    /// <param name="item">The item key.</param>
    public void Watch(string link, string item)
    {
        var pull = new WatchedPull(link, item);
        if (!_pulls.Contains(pull))
        {
            _pulls.Add(pull);
        }
    }

    /// <summary>Every instance waiting on <paramref name="link"/> stops waiting.</summary>
    /// <param name="link">The pull request's link.</param>
    public void Unwatch(string link) => _ = _pulls.RemoveAll(p => string.Equals(p.Link, link, StringComparison.Ordinal));

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WatchedPull>>> List(CancellationToken cancellationToken)
    {
        if (Failure is { } error)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<WatchedPull>>>(error);
        }

        IReadOnlyList<WatchedPull> ordered = [.. _pulls.OrderBy(static p => p.Item, StringComparer.Ordinal).ThenBy(static p => p.Link, StringComparer.Ordinal)];
        return ValueTask.FromResult(Results.Success(ordered));
    }
}
