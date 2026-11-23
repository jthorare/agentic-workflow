using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Core.Connections;

/// <summary>
/// <see cref="IConnectionCredentials"/> over the Connections page's store and secrets and the profile's secret pointers. The connection wins (#2499: the profile can still name an old token the work org rejects): the first connection of the preset, by name
/// (ordinal), that is Connected and has a credential: a secret pointer, read through <see cref="IConnectionSecrets"/>, or a `command:` credential (a gh login, first-run spec decision 14), run through <see cref="ICommandCredentials"/>.
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
        GC.KeepAlive(connection);
        return null;   // not built yet (Part of #3321): the readers' red
    }

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

        var connection = listed.Value.Where(c => c.Preset == preset && c.Status == ConnectionStatus.Connected && HasCredential(c)).OrderBy(static c => c.Name.ToString(), StringComparer.Ordinal).FirstOrDefault();
        return connection is null ? null : Read(connection);
    }

    // A command: credential runs; a secret pointer is read from the connection's secrets.
    private string? Read(Connection connection)
    {
        if (GitHubCliCredential.Of(connection) is { } command)
        {
            return commands?.Token(command);
        }

        return connection.SecretPointer is { } location && connectionSecrets?.Read(location) is { IsSuccess: true } tokens ? tokens.Value.AccessToken : null;
    }

    private bool HasCredential(Connection connection) =>
        (connection.SecretPointer is not null && connectionSecrets is not null) || (commands is not null && GitHubCliCredential.Of(connection) is not null);
}
