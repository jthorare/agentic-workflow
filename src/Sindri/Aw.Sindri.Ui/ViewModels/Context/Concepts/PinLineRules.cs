using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

#pragma warning disable MA0182 // Q7b: the concept layer uses it in the next commit
/// <summary>The §3.7 table as one pure function: where a pin stands against the approval and the map rebuild decides the card's state line and button.</summary>
internal static class PinLineRules
{
    /// <summary>The card's footer.</summary>
    /// <param name="pin">The pin as GET /sindri/context/pins gave it.</param>
    /// <param name="rebuild">Why the map is, or is not, being rebuilt.</param>
    /// <param name="problem">The first §3.8 problem line (gone features, claimed twice, nothing matches), or null.</param>
    /// <param name="moved">The what-moved line, or null.</param>
    /// <param name="zone">The zone the backoff's retry time is shown in.</param>
    /// <returns>Up to three lines and the button.</returns>
    internal static PinCardLines Of(PinView pin, MapRebuildView rebuild, string? problem, string? moved, TimeZoneInfo zone) =>
        throw new NotSupportedException();
}
#pragma warning restore MA0182
