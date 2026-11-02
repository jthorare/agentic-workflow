using Aw.Connectors.Core.GitHub;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>
/// The GitHub record feed the sync driver picks up (owned-index spec §5.2): when the profile lists GitHub repos and its token pointer resolves, a <see cref="ConnectorFeed"/> over the GitHub connector, the first pull reaching back by 'context.index.windowDays'.
/// No entry, no pointer or a pointer that doesn't resolve gives no feed (the source's status row says why).
/// </summary>
/// <param name="pointers">Resolves the token pointer.</param>
/// <param name="connectors">Makes the connector.</param>
/// <param name="readers">Makes the GitHub reader for a host, the private-host switch and the token.</param>
internal sealed class GitHubRecordFeeds(ISecretPointers pointers, IGitHubConnectorFactory connectors, Func<string?, bool, string, IGitHubReader> readers) : IExternalRecordFeeds
{
    /// <inheritdoc />
    public IReadOnlyList<RecordFeed> For(ContextSettings settings, IReadOnlyList<ContextRepo> repos, Scrubbers scrubbers)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(repos);
        if (settings.GitHub is not { Repos.Count: > 0, Token: { } pointer } github
            || SecretPointer.Parse(pointer) is not { IsSuccess: true } parsed
            || pointers.Resolve(parsed.Value) is not { IsSuccess: true } token
            || GitHubSettings.Create(github.Repos, pointer, github.Host, github.AllowPrivateHost, commentCap: null) is not { IsSuccess: true } checkedSettings)
        {
            return [];
        }

        var connector = connectors.Create(checkedSettings.Value, readers(github.Host, github.AllowPrivateHost, token.Value), TimeSpan.FromDays(settings.Index.WindowDays));
        return [new ConnectorFeed(SourceId.From("github"), connector, new ProfileRepoMap(github.Repos, repos.Select(static r => r.Name)).Place, scrubbers)];
    }
}
