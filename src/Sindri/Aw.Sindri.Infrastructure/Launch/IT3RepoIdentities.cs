namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Reads what git says a repo folder is (the T3 host's seam, so it is tested without git).</summary>
internal interface IT3RepoIdentities
{
    /// <summary>The main checkout and repository key of <paramref name="path"/>.</summary>
    /// <param name="path">The repo folder.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>What git says; nulls where it can't tell.</returns>
    public ValueTask<T3RepoIdentity> Of(string path, CancellationToken cancellationToken);
}
