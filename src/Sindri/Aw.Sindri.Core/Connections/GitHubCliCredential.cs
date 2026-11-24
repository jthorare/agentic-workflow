namespace Aw.Sindri.Core.Connections;

/// <summary>
/// The one GitHub credential a gh login gives (first-run spec decisions 13 and 14): `command:gh auth token`, run as argv with no shell. `review.credential` holds it, and so does the `github:gh` connection
/// (a <see cref="ConnectionKind.Cli"/> row with no secret pointer), which the `github` index source reads first through <see cref="ConnectionCredentials"/>. No token is copied or stored.
/// </summary>
public static class GitHubCliCredential
{
    /// <summary>The credential: gh prints its token on each call.</summary>
    public const string Command = "command:gh auth token --hostname github.com";

    /// <summary>The connection config key that holds a `command:` credential.</summary>
    public const string ConfigKey = "credential";

    /// <summary>Gets the connection the accounts step writes for a gh login: `github:gh`.</summary>
    public static ConnectionName ConnectionName { get; } = ConnectionName.Parse("github:gh").Value;

    /// <summary>The `command:` credential a connection holds in its config, for a command-line connection.</summary>
    /// <param name="connection">The connection.</param>
    /// <returns>The credential, or null when the connection holds none.</returns>
    public static string? Of(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return connection.Kind == ConnectionKind.Cli && connection.Config.TryGetValue(ConfigKey, out var credential) && credential.StartsWith("command:", StringComparison.Ordinal) ? credential : null;
    }
}
