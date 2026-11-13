using Aw.Sindri.Core.Graph.Templates;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Finds the CI a starter template can read (#3534): the connected GitHub connection plus the first profile repo's github.com origin and default branch. Red stage: a stub.</summary>
internal sealed class GitHubCiSourceLookup : ICiSourceLookup
{
    public ValueTask<GitHubCiSource?> Find(CancellationToken cancellationToken)
        => throw new NotSupportedException("the CI source lookup is not built yet");

    /// <summary>Reads owner and repo from an origin URL: https://github.com/o/r(.git) or git@github.com:o/r(.git).</summary>
    /// <param name="remoteUrl">The origin URL.</param>
    /// <returns>The owner and repo, or null for any other host or shape.</returns>
    internal static (string Owner, string Repo)? ParseRemote(string remoteUrl)
        => throw new NotSupportedException("the origin URL reader is not built yet");
}
