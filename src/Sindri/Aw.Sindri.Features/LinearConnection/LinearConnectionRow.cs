using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>Finds the one Linear connection (#3364): the Connections page's row of preset 'linear', the same pick <see cref="ConnectionCredentials"/> makes (first by name, ordinal).</summary>
internal static class LinearConnectionRow
{
    /// <summary>The name a Linear connection made from Settings or imported from the old keychain entry gets.</summary>
    internal const string DefaultName = "linear:default";

    /// <summary>Finds the Linear connection.</summary>
    /// <param name="store">The connections.</param>
    /// <param name="connectedOnly">True to skip a connection that needs sign-in or has no secret yet.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The row, or null when there is none or the store can't be read.</returns>
    internal static async ValueTask<Connection?> Find(IConnectionStore store, bool connectedOnly, CancellationToken cancellationToken)
    {
        var listed = await store.List(cancellationToken).ConfigureAwait(false);
        return listed.IsError
            ? null
            : listed.Value
                .Where(c => string.Equals(c.Preset, "linear", StringComparison.Ordinal) && (!connectedOnly || (c.Status == ConnectionStatus.Connected && c.SecretPointer is not null)))
                .OrderBy(static c => c.Name.ToString(), StringComparer.Ordinal)
                .FirstOrDefault();
    }
}
