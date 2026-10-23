using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Tests.Launch.T3;

/// <summary>What git would say about repo folders, set by the test; a folder it wasn't told about is a plain main checkout with no remote.</summary>
internal sealed class FakeT3RepoIdentities : IT3RepoIdentities
{
    private readonly Dictionary<string, T3RepoIdentity> _known = new(StringComparer.Ordinal);

    /// <summary>Says what <paramref name="path"/> is.</summary>
    /// <param name="path">The folder.</param>
    /// <param name="identity">Its main checkout and repository key.</param>
    public void Set(string path, T3RepoIdentity identity) => _known[path] = identity;

    public ValueTask<T3RepoIdentity> Of(string path, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_known.GetValueOrDefault(path) ?? new T3RepoIdentity(path, RepositoryKey: null));
}
