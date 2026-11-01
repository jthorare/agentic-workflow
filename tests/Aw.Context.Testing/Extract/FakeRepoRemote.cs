using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Context.Testing.Extract;

/// <summary>An in-memory origin remote per repo: set by the test, and a repo it was told is unreadable answers SND-CONTEXT-048. A repo it knows nothing about has no origin (null).</summary>
public sealed class FakeRepoRemote : IRepoRemote
{
    private readonly Dictionary<string, string> _origins = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unreadable = new(StringComparer.Ordinal);

    /// <summary>Sets a repo's origin URL (replacing any set before).</summary>
    /// <param name="repo">The repo's name.</param>
    /// <param name="url">The URL as git prints it.</param>
    /// <returns>This fake.</returns>
    public FakeRepoRemote Set(ContextRepoName repo, string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        _origins[repo.Value] = url;
        return this;
    }

    /// <summary>Makes a repo's checkout unreadable.</summary>
    /// <param name="repo">The repo's name.</param>
    /// <returns>This fake.</returns>
    public FakeRepoRemote Unreadable(ContextRepoName repo)
    {
        _ = _unreadable.Add(repo.Value);
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<string?>> Origin(ContextRepo repo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        return ValueTask.FromResult<Result<string?>>(_unreadable.Contains(repo.Name.Value)
            ? ContextErrors.ExtractionUnreadable.With("the fake's checkout is unreadable")
            : _origins.GetValueOrDefault(repo.Name.Value));
    }
}
