using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Connections;

/// <summary>Why the GitHub connection gave no login (#2936). The Reviews login prompt carries it so the screen can say what to do.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GitHubLoginMiss>))]
public enum GitHubLoginMiss
{
    /// <summary>No miss: the login is known, or there is no connection to blame.</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>GitHub could not be asked: a network error, a timeout, a 5xx or a rate limit. It is asked again after a minute.</summary>
    [JsonStringEnumMemberName("unreachable")]
    Unreachable,

    /// <summary>The token is refused (401), can't be read, or the connection has no token. Reconnecting fixes it.</summary>
    [JsonStringEnumMemberName("signIn")]
    SignIn,

    /// <summary>The connection's probe URL is not https://api.github.com (GitHub Enterprise); the token is never sent there. Only the typed login works.</summary>
    [JsonStringEnumMemberName("unsupportedHost")]
    UnsupportedHost,
}
