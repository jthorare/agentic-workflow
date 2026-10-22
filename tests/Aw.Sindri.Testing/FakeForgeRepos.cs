using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory <see cref="IForgeRepos"/>: the repository workspaces map to, by path.</summary>
public sealed class FakeForgeRepos : IForgeRepos
{
    private readonly Dictionary<string, PullRepo> _byWorkspace = new(StringComparer.Ordinal);

    /// <summary>Gives a workspace a repository.</summary>
    /// <param name="workspace">The workspace path.</param>
    /// <param name="repo">Its repository.</param>
    /// <returns>The same instance.</returns>
    public FakeForgeRepos Put(string workspace, PullRepo repo)
    {
        _byWorkspace[workspace] = repo;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<PullRepo>> OfWorkspace(string workspace, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<PullRepo>>(_byWorkspace.TryGetValue(workspace, out var repo) ? repo : SindriErrors.ItemNotFound.With($"{workspace} has no origin"));
}
