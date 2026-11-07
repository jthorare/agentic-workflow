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
        IReadOnlyDictionary<string, IReadOnlyList<FeaturePlace>> placesAfter) =>
        throw new NotSupportedException();
}
