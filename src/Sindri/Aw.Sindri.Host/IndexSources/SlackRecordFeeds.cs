using Aw.Connectors.Core.Slack;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>
/// The Slack record feed the sync driver picks up (owned-index spec §5.2, §5.3): when the profile lists Slack channels and a workspace and its token pointer resolves, a <see cref="ConnectorFeed"/> over the Slack connector, placed by
/// <see cref="SlackPlacement"/>, the first pull reaching back by 'context.index.windowDays'. No entry, no pointer or a pointer that doesn't resolve gives no feed, and then nothing Slack is built or registered.
/// </summary>
/// <param name="pointers">Resolves the token pointer.</param>
/// <param name="connectors">Makes the connector.</param>
/// <param name="readers">Makes the Slack reader for a token and the channel ids it may read.</param>
internal sealed class SlackRecordFeeds(ISecretPointers pointers, ISlackConnectorFactory connectors, Func<string, IReadOnlyCollection<string>, ISlackReader> readers) : IExternalRecordFeeds
{
    /// <inheritdoc />
    public IReadOnlyList<RecordFeed> For(ContextSettings settings, IReadOnlyList<ContextRepo> repos, Scrubbers scrubbers)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(repos);
        if (settings.Slack is not { Channels.Count: > 0, Token: { } pointer, Workspace: { } workspace } slack
            || SecretPointer.Parse(pointer) is not { IsSuccess: true } parsed
            || pointers.Resolve(parsed.Value) is not { IsSuccess: true } token
            || SlackSettings.Create([.. slack.Channels.Select(static c => new SlackChannelRef(c.Channel, c.Name))], pointer, workspace) is not { IsSuccess: true } checkedSettings)
        {
            return [];
        }

        var ids = checkedSettings.Value.Channels.Select(static c => c.Channel).ToList();
        var connector = connectors.Create(checkedSettings.Value, readers(token.Value, ids), TimeSpan.FromDays(settings.Index.WindowDays));
        return [new ConnectorFeed(SourceId.From("slack"), connector, new SlackPlacement(ids).Place, scrubbers)];
    }
}
