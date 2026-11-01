using System.Globalization;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The §3.7 table as one pure function: where a pin stands against the approval and the map rebuild decides the card's state line and button.</summary>
internal static class PinLineRules
{
    private const int MostLines = 3;

    /// <summary>The card's footer.</summary>
    /// <param name="pin">The pin as GET /sindri/context/pins gave it.</param>
    /// <param name="rebuild">Why the map is, or is not, being rebuilt.</param>
    /// <param name="problem">The first §3.8 problem line (gone features, claimed twice, nothing matches), or null.</param>
    /// <param name="moved">The what-moved line, or null.</param>
    /// <param name="zone">The zone the backoff's retry time is shown in.</param>
    /// <returns>Up to three lines and the button.</returns>
    internal static PinCardLines Of(PinView pin, MapRebuildView rebuild, string? problem, string? moved, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(pin);
        ArgumentNullException.ThrowIfNull(rebuild);
        ArgumentNullException.ThrowIfNull(zone);
        var applied = pin.State switch
        {
            PinStates.Waiting => new PinCardLines([Labels.PinNotApproved, Labels.PinOtherChangesWaiting], PinCardAction.ReviewAndApprove),
            PinStates.Removing => new PinCardLines([Labels.PinUnpinNotApproved, Labels.PinOtherChangesWaiting], PinCardAction.ReviewAndApprove),
            _ => Applying(pin, rebuild, zone),
        };
        return new PinCardLines([.. applied.Lines.Concat([problem, moved]).OfType<string>().Take(MostLines)], applied.Action);
    }

    // An approved pin the current map doesn't have yet is waiting on the rebuild; one the map has says nothing about it.
    private static PinCardLines Applying(PinView pin, MapRebuildView rebuild, TimeZoneInfo zone)
    {
        if (pin.Change is null)
        {
            return new PinCardLines([], PinCardAction.None);
        }

        var (rest, action) = rebuild.Phase switch
        {
            MapRebuildPhases.MapHeld => (Labels.PinRestHeld, PinCardAction.OpenReview),
            MapRebuildPhases.NoMainBranch => (Labels.PinRestNoMainBranch, PinCardAction.None),
            MapRebuildPhases.Backoff => (Labels.PinRestBackoff(rebuild.RetryAt is { } at ? TimeZoneInfo.ConvertTime(at, zone).ToString("HH:mm", CultureInfo.InvariantCulture) : "soon"), PinCardAction.TryAgain),
            MapRebuildPhases.Building => (Labels.PinRestBuilding(rebuild.Progress), PinCardAction.None),
            _ when rebuild.Failed is { } code => (Labels.PinRestFailed(code), PinCardAction.TryAgain),
            _ when rebuild.Stale => (Labels.PinRestStartsSoon, PinCardAction.None),
            _ => (null, PinCardAction.None),
        };
        return new PinCardLines(rest is null ? [] : [Labels.PinStateLine(pin.Change, rest)], action);
    }
}
