using Aw.Connectors.Core.Slack;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>
/// The Slack record feed the sync driver picks up (owned-index spec §5.2, §5.3): when the profile lists Slack channels and a workspace and a token is found (the connected Slack connection's, else the profile's pointer), a <see cref="ConnectorFeed"/> over the Slack connector, placed by
/// <see cref="SlackPlacement"/>, the first pull reaching back by 'context.index.windowDays'. No entry, or neither a connected connection nor a pointer that resolves, gives no feed, and then nothing Slack is built or registered.
/// </summary>
/// <param name="pointers">Resolves the token pointer.</param>
/// <param name="connectors">Makes the connector.</param>
/// <param name="readers">Makes the Slack reader for a token and the channel ids it may read.</param>
/// <param name="connections">The Connections page's rows (the Slack connection whose token the feed reads with first), or null for none.</param>
/// <param name="connectionSecrets">Reads a connection's tokens, or null for none.</param>
internal sealed class SlackRecordFeeds(ISecretPointers pointers, ISlackConnectorFactory connectors, Func<string, IReadOnlyCollection<string>, ISlackReader> readers, IConnectionStore? connections = null, IConnectionSecrets? connectionSecrets = null) : IExternalRecordFeeds
{
    private readonly ConnectionCredentials _credentials = new(connections, connectionSecrets, pointers);

    /// <inheritdoc />
    public IReadOnlyList<RecordFeed> For(ContextSettings settings, IReadOnlyList<ContextRepo> repos, Scrubbers scrubbers)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(repos);
        if (settings.Slack is not { Channels.Count: > 0, Workspace: { } workspace } slack
            || _credentials.TokenFor("slack", slack.Token) is not { } token
            || SlackSettings.Create([.. slack.Channels.Select(static c => new SlackChannelRef(c.Channel, c.Name))], slack.Token, workspace) is not { IsSuccess: true } checkedSettings)
        {
            return [];
        }

        var ids = checkedSettings.Value.Channels.Select(static c => c.Channel).ToList();
        var connector = connectors.Create(checkedSettings.Value, readers(token, ids), TimeSpan.FromDays(settings.Index.WindowDays));
        return [new ConnectorFeed(SourceId.From("slack"), connector, new SlackPlacement(ids).Place, scrubbers)];
    }
}
