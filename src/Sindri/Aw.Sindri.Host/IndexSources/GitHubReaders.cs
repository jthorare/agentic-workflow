using Aw.Connectors.Core.GitHub;
using Aw.Connectors.Infrastructure;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>Makes the GitHub reader 'index connect' checks a token with: the Connectors package's own, on a handler that follows no redirect and refuses private hosts the profile doesn't allow.</summary>
internal static class GitHubReaders
{
    internal static IGitHubReader Create(string? host, bool allowPrivateHost, string token) =>
        new ServiceCollection().AddGitHubReader(host, allowPrivateHost, token).BuildServiceProvider().GetRequiredService<IGitHubReader>();
}
