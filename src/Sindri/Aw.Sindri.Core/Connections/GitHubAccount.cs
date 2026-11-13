namespace Aw.Sindri.Core.Connections;

/// <summary>The GitHub account the Reviews tab reads with.</summary>
/// <param name="State">Where the connection stands.</param>
/// <param name="Connection">The connection's name ('github:work'); null when there is none.</param>
/// <param name="Login">The GitHub login from the connection's /user call; null when unknown.</param>
/// <param name="Miss">Why <paramref name="Login"/> is null; <see cref="GitHubLoginMiss.None"/> when it is known or there is no connection.</param>
public sealed record GitHubAccount(GitHubAccountState State, string? Connection, string? Login, GitHubLoginMiss Miss = GitHubLoginMiss.None);
