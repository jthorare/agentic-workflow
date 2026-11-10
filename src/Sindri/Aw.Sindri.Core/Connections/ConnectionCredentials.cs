using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Core.Connections;

/// <summary>
/// <see cref="IConnectionCredentials"/> over the Connections page's store and secrets and the profile's secret pointers. The connection wins (#2499: the profile can still name an old token the work org rejects): the first connection of the preset, by name
/// (ordinal), that is Connected and has a secret pointer, read through <see cref="IConnectionSecrets"/>. A NeedsSignIn connection doesn't count. A null store or secrets means no connection to read, and then only the pointer is tried.
/// </summary>
/// <param name="connections">The Connections page's rows, or null for none.</param>
/// <param name="connectionSecrets">Reads a connection's tokens, or null for none.</param>
/// <param name="pointers">Resolves the profile's token pointer.</param>
public sealed class ConnectionCredentials(IConnectionStore? connections, IConnectionSecrets? connectionSecrets, ISecretPointers pointers) : IConnectionCredentials
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

    private string? FromConnection(string preset)
    {
        if (connections is null || connectionSecrets is null)
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

        var connection = listed.Value.Where(c => c.Preset == preset && c.Status == ConnectionStatus.Connected && c.SecretPointer is not null).OrderBy(static c => c.Name.ToString(), StringComparer.Ordinal).FirstOrDefault();
        return connection?.SecretPointer is { } location && connectionSecrets.Read(location) is { IsSuccess: true } tokens ? tokens.Value.AccessToken : null;
    }
}
