namespace Aw.Context.Core.Engine;

/// <summary>'context.sources.github', checked (owned index spec §5.3, §5.4). The token is a secret pointer and <see cref="ToString"/> withholds it.</summary>
public sealed class GitHubSource
{
    /// <summary>The repos read, 'owner/name'.</summary>
    public IReadOnlyList<string> Repos { get; init; } = [];

    /// <summary>The Enterprise API host (a host name, optionally with a port, over https), or null for api.github.com.</summary>
    public string? Host { get; init; }

    /// <summary>True when the profile marks a private, loopback or link-local host as intended.</summary>
    public bool AllowPrivateHost { get; init; }

    /// <summary>The secret pointer to the read-only token, or null before 'sindri index connect github'.</summary>
    public string? Token { get; init; }

    /// <summary>The source without the token pointer.</summary>
    /// <returns>A description.</returns>
    public override string ToString() => $"GitHubSource {{ Repos = {string.Join(',', Repos)}, Host = {Host}, AllowPrivateHost = {AllowPrivateHost}, Token = (withheld) }}";
}
