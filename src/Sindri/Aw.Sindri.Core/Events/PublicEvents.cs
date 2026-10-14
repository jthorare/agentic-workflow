using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Events;

/// <summary>Maps a ledger row to the <see cref="PublicEvent"/> the stream and ledger reads send (API spec §3.6, §5.3).</summary>
public static class PublicEvents
{
    /// <summary>
    /// The public form of <paramref name="row"/>: null when its type isn't public and <paramref name="includeNonPublic"/> is
    /// false; else the envelope with the catalog's public payload (or, for a non-public type, the whole payload), every
    /// string escaped.
    /// </summary>
    /// <param name="row">The ledger row.</param>
    /// <param name="options">The options that know each public payload type.</param>
    /// <param name="includeNonPublic">True for a human's ledger read.</param>
    /// <returns>The event, or null.</returns>
    public static Result<PublicEvent?> For(LedgerEvent row, JsonSerializerOptions options, bool includeNonPublic)
        => throw new NotSupportedException("PublicEvents.For: P5 unit A makes the public envelope tests pass");
}
