namespace Aw.Sindri.Core.Messages;

/// <summary>
/// The only hosts a Messages token is sent to (messages spec §11): https on slack.com or a host under it (files.slack.com too), no user info. A copy of
/// the Connectors' internal SlackHostPolicy with the same test cases (§8.1, "Dependencies"); the one other host is the fixture's loopback base, which
/// <see cref="SlackBaseUrlRule"/> alone allows.
/// </summary>
public static class SlackHostRule
{
    /// <summary>True when <paramref name="url"/> is https, has no user info and a host of slack.com or '*.slack.com'.</summary>
    /// <param name="url">A url the client is about to request.</param>
    /// <returns>Whether the token may be sent to it.</returns>
    public static bool IsSlack(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return url.IsAbsoluteUri
            && url.Scheme == Uri.UriSchemeHttps
            && url.UserInfo.Length == 0
            && url.HostNameType == UriHostNameType.Dns
            && (string.Equals(url.Host, "slack.com", StringComparison.OrdinalIgnoreCase) || url.Host.EndsWith(".slack.com", StringComparison.OrdinalIgnoreCase));
    }
}
