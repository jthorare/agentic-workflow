using Aw.Kernel;

namespace Aw.Sindri.Core.Setup;

/// <summary>The repos the owner worked on in GitHub lately (first-run spec gap 7): one read of the account's recent repos, so the scan can tick the ones they know.</summary>
public interface IKnownRepos
{
    /// <summary>Reads the slugs ('owner/name', lower case) of the account's recent repos; none when no GitHub account is connected.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The slugs, or the read's error (the scan then ticks nothing and says so).</returns>
    public ValueTask<Result<IReadOnlySet<string>>> Slugs(CancellationToken cancellationToken);
}
