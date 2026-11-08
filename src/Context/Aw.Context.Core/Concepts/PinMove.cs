using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Concepts;

/// <summary>What one pin gained and lost between two map versions (the pins read's 'moved' field): the features that came into it, and where the ones that left it went.</summary>
/// <param name="Pin">The pin's id.</param>
/// <param name="FromVersion">The version before the refresh.</param>
/// <param name="ToVersion">The version the refresh published.</param>
/// <param name="Gained">Features that belong to the pin now and did not before.</param>
/// <param name="Lost">Features that belonged to the pin before and do not now.</param>
/// <param name="To">Where the lost features went, the place that took the most first, at most <see cref="PinMoves.MaxPlaces"/>.</param>
public sealed record PinMove(ConceptId Pin, int FromVersion, int ToVersion, int Gained, int Lost, IReadOnlyList<PinMoveTo> To);
