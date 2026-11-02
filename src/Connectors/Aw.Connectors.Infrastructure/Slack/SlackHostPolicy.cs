namespace Aw.Connectors.Infrastructure.Slack;

/// <summary>The only hosts the Slack client talks to: https on slack.com or a host under it (owned index spec §10, an exact-match allowlist).</summary>
internal static class SlackHostPolicy
{
    /// <summary>True when <paramref name="url"/> is https, has no user info and a host of slack.com or '*.slack.com'.</summary>
    /// <param name="url">A url the client is about to request.</param>
    /// <returns>Whether the request may be sent.</returns>
    public static bool IsAllowed(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return url.IsAbsoluteUri
            && url.Scheme == Uri.UriSchemeHttps
            && url.UserInfo.Length == 0
            && url.HostNameType == UriHostNameType.Dns
            && (string.Equals(url.Host, "slack.com", StringComparison.OrdinalIgnoreCase) || url.Host.EndsWith(".slack.com", StringComparison.OrdinalIgnoreCase));
    }
}
