using Aw.Connectors.Core;
using Aw.Context.Core.Records;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>
/// Where a Slack record belongs (owned-index spec §4.4): in no repo, and visible to the people of its channel, audience 'slack:&lt;channelId&gt;', read from the record key 'slack:&lt;channelId&gt;/…'. A record of a channel the profile
/// doesn't list has no placement and is dropped (default deny).
/// </summary>
/// <param name="listed">The channel ids 'context.sources.slack.channels' lists.</param>
internal sealed class SlackPlacement(IEnumerable<string> listed)
{
    private const string Prefix = "slack:";
    private readonly HashSet<string> _listed = new(listed, StringComparer.Ordinal);

    /// <summary>Places a Slack record.</summary>
    /// <param name="item">The record.</param>
    /// <returns>The placement, or null to skip the record (a key that is not a Slack key, or an unlisted channel).</returns>
    internal Placement? Place(ConnectorItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var slash = item.Key.IndexOf('/', StringComparison.Ordinal);
        if (!item.Key.StartsWith(Prefix, StringComparison.Ordinal) || slash <= Prefix.Length)
        {
            return null;
        }

        var channel = item.Key[Prefix.Length..slash];
        return _listed.Contains(channel) && AudienceKey.TryFrom(Prefix + channel, out var audience) ? new Placement(Repo: null, audience, SourceRepo: null) : null;
    }
}
