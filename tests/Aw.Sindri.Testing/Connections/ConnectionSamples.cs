using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Secrets;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Testing.Connections;

/// <summary>Connections and names for tests. Names are built without <see cref="ConnectionName.Parse"/>, so a test of the store does not depend on the parser.</summary>
public static class ConnectionSamples
{
    /// <summary>A name from its two parts.</summary>
    /// <param name="service">The service part.</param>
    /// <param name="label">The label part.</param>
    /// <returns>The name.</returns>
    public static ConnectionName Name(string service = "linear", string label = "work") => new() { Service = service, Label = label };

    /// <summary>A connected oauth connection with a keychain secret pointer.</summary>
    /// <param name="name">The name; 'linear:work' when null.</param>
    /// <param name="status">The status.</param>
    /// <returns>The connection.</returns>
    public static Connection Oauth(ConnectionName? name = null, ConnectionStatus status = ConnectionStatus.Connected)
    {
        var used = name ?? Name();
        return new Connection(
            used,
            ConnectionKind.OAuth,
            Preset: used.Service,
            Config: new Dictionary<string, string>(StringComparer.Ordinal) { ["clientId"] = "client-123", ["scopes"] = "read write" },
            SecretPointer: SecretPointer.Parse($"keychain:sindri-connection/{used.Service}.{used.Label}").Value,
            status,
            StatusReason: status == ConnectionStatus.Connected ? null : "the provider refused the refresh",
            CreatedBy: "human",
            CreatedAt: KnownInstants.Epoch,
            UpdatedAt: KnownInstants.Epoch.AddMinutes(1),
            LastRefreshedAt: null);
    }
}
