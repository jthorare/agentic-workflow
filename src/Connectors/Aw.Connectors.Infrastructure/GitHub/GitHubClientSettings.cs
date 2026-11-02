namespace Aw.Connectors.Infrastructure.GitHub;

/// <summary>
/// What the GitHub client needs from the profile's 'context.sources.github': the Enterprise host, whether a private host is intended and the
/// already resolved token. The Host resolves the secret pointer and passes the token; <see cref="ToString"/> withholds it.
/// </summary>
internal sealed class GitHubClientSettings
{
    /// <summary>An Enterprise API host (a host name, optionally with a port; reached over https at '/api/v3'), or null for api.github.com.</summary>
    public string? Host { get; init; }

    /// <summary>True when the profile marks an IP-literal, private, loopback or link-local host as intended.</summary>
    public bool AllowPrivateHost { get; init; }

    /// <summary>The read-only token, or null to call unauthenticated (public repos, 60 requests an hour). Never logged and never in an error.</summary>
    public string? Token { get; init; }

    /// <summary>The settings without the token.</summary>
    /// <returns>A description.</returns>
    public override string ToString() => $"GitHubClientSettings {{ Host = {Host}, AllowPrivateHost = {AllowPrivateHost}, Token = (withheld) }}";
}
