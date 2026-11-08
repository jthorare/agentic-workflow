using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Concepts;

/// <summary>
/// Which features each pin gained and lost when a refresh published a new map version. A pin that is new in the later version gained all its features (it has
/// no earlier members to lose); a pin the later version no longer has gets no entry (the pins read does not list it).
/// </summary>
public static class PinMoves
{
    /// <summary>The most places a move names.</summary>
    public const int MaxPlaces = 2;

    /// <summary>The moves of every pin the later version has.</summary>
    /// <param name="fromVersion">The earlier version's number.</param>
    /// <param name="toVersion">The later version's number.</param>
    /// <param name="before">Each pin's id to the feature keys that belonged to it in the earlier version.</param>
    /// <param name="after">Each pin's id to the feature keys that belong to it in the later version.</param>
    /// <param name="placesAfter">A feature key to the concepts it belongs to in the later version (a lost feature goes to the first that is not the pin itself).</param>
    /// <returns>One move per pin in <paramref name="after"/>, in its order.</returns>
    public static IReadOnlyList<PinMove> Between(
        int fromVersion,
        int toVersion,
        IReadOnlyDictionary<string, IReadOnlySet<string>> before,
        IReadOnlyDictionary<string, IReadOnlySet<string>> after,
        IReadOnlyDictionary<string, IReadOnlyList<FeaturePlace>> placesAfter)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(placesAfter);
        var moves = new List<PinMove>();
        foreach (var (pin, now) in after)
        {
            if (!before.TryGetValue(pin, out var was))
            {
                moves.Add(new PinMove(ConceptId.From(pin), fromVersion, toVersion, now.Count, 0, []));
                continue;
            }

            var lost = was.Where(f => !now.Contains(f)).ToList();
            moves.Add(new PinMove(ConceptId.From(pin), fromVersion, toVersion, now.Count(f => !was.Contains(f)), lost.Count, Places(pin, lost, placesAfter)));
        }

        return moves;
    }

    private static List<PinMoveTo> Places(string pin, List<string> lost, IReadOnlyDictionary<string, IReadOnlyList<FeaturePlace>> placesAfter) =>
        [.. lost
            .Select(f => placesAfter.TryGetValue(f, out var places) ? places.FirstOrDefault(p => p.Id.Value != pin) : null)
            .GroupBy(p => p?.Id.Value, StringComparer.Ordinal)
            .Select(g => new PinMoveTo(g.First()?.Id, g.First()?.Name, g.Count()))
            .OrderByDescending(t => t.Features)
            .ThenBy(t => t.Name ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(t => t.Id?.Value ?? string.Empty, StringComparer.Ordinal)
            .Take(MaxPlaces)];
}
