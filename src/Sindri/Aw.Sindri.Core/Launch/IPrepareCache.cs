using Aw.Kernel;

namespace Aw.Sindri.Core.Launch;

/// <summary>The installed <c lang="text">node_modules</c> of a package directory, kept by <see cref="PrepareKey"/> under the real home's <c lang="text">cache/prepare/&lt;key&gt;/</c> (sandbox spec §3.8).</summary>
public interface IPrepareCache
{
    /// <summary>Whether the cache holds an install for <paramref name="key"/>.</summary>
    /// <param name="key">The <see cref="PrepareKey"/>.</param>
    /// <returns>True when it does.</returns>
    public bool Has(string key);

    /// <summary>Copies the cached <c lang="text">node_modules</c> into <paramref name="packageDirectory"/> (reflink where the filesystem has it, else a plain copy).</summary>
    /// <param name="key">The <see cref="PrepareKey"/>.</param>
    /// <param name="packageDirectory">The package directory in the worktree.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>Success, or the copy's error.</returns>
    public ValueTask<Result<Unit>> Restore(string key, string packageDirectory, CancellationToken cancellationToken);

    /// <summary>Fills the cache for <paramref name="key"/> from the <c lang="text">node_modules</c> an install just made in <paramref name="packageDirectory"/>.</summary>
    /// <param name="key">The <see cref="PrepareKey"/>.</param>
    /// <param name="packageDirectory">The package directory in the worktree.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>Success, or the copy's error.</returns>
    public ValueTask<Result<Unit>> Store(string key, string packageDirectory, CancellationToken cancellationToken);
}
