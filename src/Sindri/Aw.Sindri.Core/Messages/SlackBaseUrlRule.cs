namespace Aw.Sindri.Core.Messages;

/// <summary>
/// The AW_SLACK_BASE_URL rule (messages spec §8.1): the override is taken only when the host is loopback, the Sindri home holds the marker
/// '.slack-fixture-home' (line 1 'slack-fixture v1', line 2 the port), the URL's port is the marker's and the token starts with 'xoxp-fixture-';
/// otherwise the API base is https://slack.com/api/. M1d builds it.
/// </summary>
public static class SlackBaseUrlRule
{
    /// <summary>Slack's own API base.</summary>
    public static Uri Slack { get; } = new("https://slack.com/api/");

    /// <summary>The API base to call.</summary>
    /// <param name="overrideUrl">AW_SLACK_BASE_URL's value, or null.</param>
    /// <param name="marker">The marker file's text, or null when the home has none.</param>
    /// <param name="token">The connection's token: only its prefix is read.</param>
    /// <returns>The base URL.</returns>
    public static Uri Resolve(string? overrideUrl, string? marker, string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        _ = (overrideUrl, marker);
        throw MessageStubs.NotBuilt("SlackBaseUrlRule.Resolve", "M1d");
    }
}
