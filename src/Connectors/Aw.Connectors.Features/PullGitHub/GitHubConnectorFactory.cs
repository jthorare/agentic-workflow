using Aw.Connectors.Core;
using Aw.Connectors.Core.GitHub;
using Aw.Kernel.Services;

namespace Aw.Connectors.Features.PullGitHub;

/// <summary>Makes <see cref="GitHubConnector"/>.</summary>
internal sealed class GitHubConnectorFactory(TimeProvider clock) : IGitHubConnectorFactory
{
    /// <inheritdoc />
    public IConnector Create(GitHubSettings settings, IGitHubReader reader, TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(reader);
        var pull = new ValidatingService<PullGitHubRequest, PullGitHubResponse>(new PullGitHubService(reader), new PullGitHubValidator());
        return new GitHubConnector(settings.Repos, settings.CommentCap, window, pull, reader, clock);
    }
}
