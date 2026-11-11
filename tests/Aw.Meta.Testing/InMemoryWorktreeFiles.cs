using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IWorktreeFiles: the one fake, kept honest by WorktreeFilesContract.</summary>
public sealed class InMemoryWorktreeFiles : IWorktreeFiles
{
    private readonly Dictionary<string, Dictionary<string, DateTimeOffset>> _trees = new(StringComparer.Ordinal);

    private readonly Dictionary<string, string> _roots = new(StringComparer.Ordinal);

    /// <summary>Sets the worktree root configured for a repo (what the environment or worktrees.json would give).</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="root">The raw root text.</param>
    public void ConfigureRoot(string repo, string root)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(root);
        _roots[repo] = root;
    }

    /// <summary>When set, every call fails with this error.</summary>
    public Error? Failure { get; set; }

    /// <summary>Sets the modification times of a directory's entries (relative path to time; "" is the directory itself).</summary>
    /// <param name="directory">The directory.</param>
    /// <param name="entries">The entries.</param>
    public void Arrange(string directory, IReadOnlyDictionary<string, DateTimeOffset> entries)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(entries);
        _trees[directory] = new Dictionary<string, DateTimeOffset>(entries, StringComparer.Ordinal);
    }

    /// <summary>Sets just the directory's own time (a tree with nothing else in it).</summary>
    /// <param name="directory">The directory.</param>
    /// <param name="time">Its modification time.</param>
    public void Touch(string directory, DateTimeOffset time) => Arrange(directory, new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal) { [string.Empty] = time });

    /// <inheritdoc />
    public ValueTask<Result<DateTimeOffset>> NewestChange(string directory, IReadOnlyCollection<string> skipNames, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skipNames);
        if (Failure is { } error)
        {
            return Done.Failed<DateTimeOffset>(error);
        }

        if (!_trees.TryGetValue(directory, out var entries))
        {
            return Done.Failed<DateTimeOffset>(MetaErrors.CommandFailed.With("no such directory " + directory));
        }

        var newest = entries.Where(e => Counts(e.Key, skipNames)).Select(e => e.Value).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
        return Done.Of(newest);
    }

    /// <inheritdoc />
    public ValueTask<Result<string?>> ConfiguredRoot(string repo, CancellationToken cancellationToken) =>
        Failure is { } error ? Done.Failed<string?>(error) : Done.Of(_roots.TryGetValue(repo, out var root) ? root : null);

    private static bool Counts(string relative, IReadOnlyCollection<string> skipNames)
    {
        var parts = relative.Length == 0 ? [] : relative.Split('/');
        return parts.Length <= 2 && !parts.Any(part => skipNames.Contains(part, StringComparer.Ordinal));
    }
}
