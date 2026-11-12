using System.Text.Json;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>Reads a string of the live profile's 'context.sources.linear' entry as written, without checking the rest of the settings.</summary>
internal static class LinearProfileText
{
    /// <summary>Reads 'context.sources.linear.&lt;name&gt;'.</summary>
    /// <param name="live">The live profile.</param>
    /// <param name="name">The key under the linear entry ('token', 'writeToken', 'url').</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The string, or null when the profile, the entry or the key is missing or not a string.</returns>
    internal static async ValueTask<string?> Raw(IProfileSource live, string name, CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        var at = loaded.IsError ? null : loaded.Value.Profile.Context;
        foreach (var step in new[] { "sources", "linear", name })
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
