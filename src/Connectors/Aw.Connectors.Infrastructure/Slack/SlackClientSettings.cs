namespace Aw.Connectors.Infrastructure.Slack;

/// <summary>
/// What the Slack client needs from the profile's 'context.sources.slack': the channel ids it may read and the already resolved bot token. The Host resolves the
/// secret pointer and passes the token; <see cref="ToString"/> withholds it.
/// </summary>
internal sealed class SlackClientSettings
{
    /// <summary>The bot token, never logged and never in an error.</summary>
    public string? Token { get; init; }

    /// <summary>The channel ids a request may name (the profile's allowlist). Any other id is refused before a request is sent.</summary>
    public IReadOnlyCollection<string> Channels { get; init; } = [];

    /// <summary>The settings without the token.</summary>
    /// <returns>A description.</returns>
    public override string ToString() => $"SlackClientSettings {{ Channels = {Channels.Count}, Token = (withheld) }}";
}
