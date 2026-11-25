using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Core.Connections;

/// <summary>
/// <see cref="IConnectionCredentials"/> over the Connections page's store and secrets and the profile's secret pointers. The connection wins (#2499: the profile can still name an old token the work org rejects): the newest Connected connection of the preset (<see cref="Newest"/>)
/// whose credential reads: a secret pointer, read through <see cref="IConnectionSecrets"/>, or a `command:` credential (a gh login, first-run spec decision 14), run through <see cref="ICommandCredentials"/>.
/// A NeedsSignIn connection doesn't count. A null store means no connection to read, and then only the pointer is tried.
/// </summary>
/// <param name="connections">The Connections page's rows, or null for none.</param>
/// <param name="connectionSecrets">Reads a connection's tokens, or null for none.</param>
/// <param name="pointers">Resolves the profile's token pointer.</param>
/// <param name="commands">Runs a connection's `command:` credential, or null when none may run.</param>
public sealed class ConnectionCredentials(IConnectionStore? connections, IConnectionSecrets? connectionSecrets, ISecretPointers pointers, ICommandCredentials? commands = null) : IConnectionCredentials
{
    /// <inheritdoc />
    public string? TokenFor(string preset, string? profilePointer)
    {
        ArgumentException.ThrowIfNullOrEmpty(preset);
        if (FromConnection(preset) is { } fromConnection)
        {
            return fromConnection;
        }

        return profilePointer is not null && SecretPointer.Parse(profilePointer) is { IsSuccess: true } parsed && pointers.Resolve(parsed.Value) is { IsSuccess: true } resolved
            ? resolved.Value
            : null;
    }

    /// <inheritdoc />
    public string? TokenOf(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (GitHubCliCredential.Of(connection) is { } command)
        {
            return commands?.Token(command);
        }

        return connection.SecretPointer is { } location && connectionSecrets?.Read(location) is { IsSuccess: true } tokens ? tokens.Value.AccessToken : null;
    }

    /// <summary>
    /// The order every reader of a preset's connections tries them in (first-run spec decision 14 is silent on two connected rows, so the lead's default applies): the most recently added first, by
    /// <see cref="Connection.CreatedAt"/>, then by name (ordinal). Not by UpdatedAt: a token refresh moves it, and the account would swap back and forth.
    /// </summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The rows, newest first.</returns>
    public static IEnumerable<Connection> Newest(IEnumerable<Connection> rows) =>
        rows.OrderByDescending(static c => c.CreatedAt).ThenBy(static c => c.Name.ToString(), StringComparer.Ordinal);

    private string? FromConnection(string preset)
    {
        if (connections is null)
        {
            return null;
        }

#pragma warning disable RS0030 // the feeds' IExternalRecordFeeds.For is synchronous and the connection list is one local SQLite read; the host has no synchronization context to deadlock on (the one blocking read, kept here)
        var listed = connections.List(CancellationToken.None).AsTask().GetAwaiter().GetResult();
#pragma warning restore RS0030
        if (listed.IsError)
        {
            return null;
        }

        // the newest connected row whose token reads: a signed-out gh login doesn't hide an OAuth row behind it
        return Newest(listed.Value.Where(c => c.Preset == preset && c.Status == ConnectionStatus.Connected)).Select(TokenOf).FirstOrDefault(static t => t is { Length: > 0 });
    }
}
