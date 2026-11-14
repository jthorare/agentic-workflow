using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.AddConnection;

/// <summary>
/// The connection names the generic add and rename refuse. slack:messages has no preset on purpose and is made only by sindri messages connect (#3279, #3640),
/// so neither slice may create it, and a rename must not move it away.
/// </summary>
internal static class ReservedConnectionNames
{
    /// <summary>Parses a name, refusing the reserved one with SND-CONN-017.</summary>
    /// <param name="text">The name as typed, 'service:label'.</param>
    /// <returns>The name, or the refusal.</returns>
    internal static Result<ConnectionName> Parse(string text) =>
        string.Equals(text, "slack:messages", StringComparison.Ordinal)
            ? SindriErrors.ConnectionNameReserved.With("slack:messages is made by sindri messages connect (fix: run sindri messages connect)")
            : ConnectionName.Parse(text);
}
