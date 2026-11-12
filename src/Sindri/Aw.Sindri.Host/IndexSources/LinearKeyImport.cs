using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>
/// The one-time import of the old Linear key (#3364). Linear has one connection, the Connections page's. A home that connected Linear in Settings before has the key in the keychain behind 'context.sources.linear.token' and no
/// connection, so on start that key becomes the connection 'linear:default' (secret and row) and one 'connection.imported' event is written. A home that already has a Linear connection, or no readable old key, changes nothing, so a second start imports nothing.
/// </summary>
/// <param name="live">The live profile.</param>
/// <param name="pointers">Reads the pointer's secret.</param>
/// <param name="connections">The Connections page's rows.</param>
/// <param name="secrets">Keeps the connection's key.</param>
/// <param name="presets">The shipped presets, for the Linear one's kind and probe address.</param>
/// <param name="events">Writes the ledger event.</param>
/// <param name="clock">Stamps the row.</param>
internal sealed class LinearKeyImport(IProfileSource live, ISecretPointers pointers, IConnectionStore connections, IConnectionSecrets secrets, IConnectionPresetCatalog presets, ConnectionEvents events, TimeProvider clock)
{
    /// <summary>Imports the old key when there is one and no Linear connection.</summary>
    /// <param name="cancellationToken">Cancels the import.</param>
    /// <returns>True when a connection was made; false when nothing needed importing; the error when a store failed (the caller tries again).</returns>
    internal async ValueTask<Result<bool>> Run(CancellationToken cancellationToken)
    {
        var listed = await connections.List(cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        if (listed.Value.Any(static c => string.Equals(c.Preset, "linear", StringComparison.Ordinal)))
        {
            return false;
        }

        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError || Pointer(loaded.Value.Profile.Context) is not { } text || SecretPointer.Parse(text) is not { IsSuccess: true } parsed || pointers.Resolve(parsed.Value) is not { IsSuccess: true } key)
        {
            return false;   // why: no old key, or one that doesn't resolve here (a locked keychain): nothing to carry over, and the owner can connect on the Connections page
        }

        var name = ConnectionName.Parse("linear:default").Value;
        var stored = secrets.Store(name, new TokenSet(key.Value, RefreshToken: null, ExpiresAt: null));
        if (stored.IsError)
        {
            return stored.Error;
        }

        var now = clock.GetUtcNow();
        var preset = presets.Find("linear");
        var config = preset?.ProbeUrl is { } probe ? new Dictionary<string, string>(StringComparer.Ordinal) { ["probeUrl"] = probe } : [];
        var row = new Connection(name, preset?.Kind ?? ConnectionKind.Rest, "linear", config, stored.Value, ConnectionStatus.Connected, StatusReason: null, "import", now, now, LastRefreshedAt: null);
        var written = await connections.Insert(row, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            _ = secrets.Delete(stored.Value);
            return written.Error;
        }

        await events.Imported(name, text, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static string? Pointer(JsonElement? context)
    {
        var at = context;
        foreach (var step in new[] { "sources", "linear", "token" })
        {
            if (at is not { ValueKind: JsonValueKind.Object } node || !node.TryGetProperty(step, out var next))
            {
                return null;
            }

            at = next;
        }

        return at is { ValueKind: JsonValueKind.String } text ? text.GetString() : null;
    }
}
