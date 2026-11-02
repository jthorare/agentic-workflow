namespace Aw.Connectors.Core.Slack;

/// <summary>Makes the Slack connector for the profile's channels over a reader the caller built (the reader carries the token, which only the Host resolves).</summary>
public interface ISlackConnectorFactory
{
    /// <summary>Makes the connector.</summary>
    /// <param name="settings">The checked 'context.sources.slack' entry; its channels are the ones read, in order.</param>
    /// <param name="reader">Reads Slack; it refuses any channel it was not built for.</param>
    /// <param name="window">How far back a channel's first pull reaches.</param>
    /// <returns>A connector that pulls every listed channel, each from its own watermark.</returns>
    public IConnector Create(SlackSettings settings, ISlackReader reader, TimeSpan window);
}
