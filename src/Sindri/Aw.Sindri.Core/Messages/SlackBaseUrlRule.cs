using System.Globalization;

namespace Aw.Sindri.Core.Messages;

/// <summary>
/// The AW_SLACK_BASE_URL rule (messages spec §8.1): the override is taken only when the host is loopback, the Sindri home holds the marker
/// '.slack-fixture-home' (line 1 'slack-fixture v1', line 2 the port), the URL's port is the marker's and the token starts with 'xoxp-fixture-';
/// otherwise the API base is https://slack.com/api/. So a stray variable can't send a real token off slack.com.
/// </summary>
public static class SlackBaseUrlRule
{
    /// <summary>The marker file's name in the Sindri home; only 'scripts/slack-fixture.sh up' writes it.</summary>
    public const string MarkerFile = ".slack-fixture-home";

    /// <summary>The marker's first line.</summary>
    public const string MarkerHeader = "slack-fixture v1";

    /// <summary>The prefix every fixture token starts with; a real-looking token is never sent to the override.</summary>
    public static string FixtureTokenPrefix { get; } = SlackUserToken.Prefix + "fixture-";

    /// <summary>Slack's own API base.</summary>
    public static Uri Slack { get; } = new("https://slack.com/api/");

    /// <summary>The API base to call.</summary>
    /// <param name="overrideUrl">AW_SLACK_BASE_URL's value, or null.</param>
    /// <param name="marker">The marker file's text, or null when the home has none.</param>
    /// <param name="token">The connection's token: only its prefix is read.</param>
    /// <returns>The override (ending in '/') when all four conditions hold, else <see cref="Slack"/>.</returns>
    public static Uri Resolve(string? overrideUrl, string? marker, string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (string.IsNullOrWhiteSpace(overrideUrl) || marker is null || !token.StartsWith(FixtureTokenPrefix, StringComparison.Ordinal)
            || !Uri.TryCreate(overrideUrl.Trim(), UriKind.Absolute, out var url) || !IsLoopbackHttp(url) || MarkerPort(marker) is not { } port || url.Port != port)
        {
            return Slack;
        }

        return url.AbsolutePath.EndsWith('/') ? url : new Uri(url.GetLeftPart(UriPartial.Path) + "/");
    }

    // http or https, no user info, no query or fragment, and a host of exactly 127.0.0.1, ::1 or localhost (not the rest of 127/8).
    private static bool IsLoopbackHttp(Uri url) =>
        (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
        && url.UserInfo.Length == 0
        && url.Query.Length == 0
        && url.Fragment.Length == 0
        && (string.Equals(url.Host, "127.0.0.1", StringComparison.Ordinal)
            || string.Equals(url.Host, "[::1]", StringComparison.Ordinal)
            || string.Equals(url.Host, "localhost", StringComparison.OrdinalIgnoreCase));

    // Line 1 is the header, line 2 a port from 1 to 65535; anything else is no marker.
    private static int? MarkerPort(string marker)
    {
        var lines = marker.Split('\n');
        return lines.Length >= 2
            && string.Equals(lines[0].TrimEnd('\r'), MarkerHeader, StringComparison.Ordinal)
            && int.TryParse(lines[1].TrimEnd('\r'), NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            && port is > 0 and <= 65535
                ? port
                : null;
    }
}
