using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The §3.8 problem line of a pinned card: the first thing wrong with how its pin meets the map.</summary>
internal static class PinCoverageRules
{
    /// <summary>The first problem line, or null.</summary>
    /// <param name="pin">The pin.</param>
    /// <param name="card">Its card on the map; null while the map has no node for it.</param>
    /// <param name="earlier">The pins listed before this one, in profile order.</param>
    /// <param name="mapFeatures">The keys of the map's features; null when the layer doesn't know them (no feature is called gone then).</param>
    /// <returns>The gone-features, nothing-matches or claimed-twice line.</returns>
    internal static string? Problem(PinView pin, ConceptTileViewModel? card, IReadOnlyList<PinView> earlier, IReadOnlySet<string>? mapFeatures)
    {
        ArgumentNullException.ThrowIfNull(pin);
        ArgumentNullException.ThrowIfNull(earlier);
        if (mapFeatures is not null && pin.Features.Count(f => !mapFeatures.Contains(f)) is > 0 and var gone)
        {
            return Labels.PinFeaturesGone(gone);
        }

        if (card is { Size.Total: 0 })
        {
            return Labels.PinMatchesNothing;
        }

        foreach (var owner in earlier)
        {
            if (pin.Features.Intersect(owner.Features, StringComparer.Ordinal).Count() is > 0 and var shared)
            {
                return Labels.PinClaimedTwice(shared, owner.Name);
            }
        }

        return null;
    }
}
