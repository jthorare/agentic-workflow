using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>Pins that can be swapped, so a test can change a lock row between two runs.</summary>
/// <param name="pins">The pins it starts with.</param>
public sealed class FakeOnyxPins(OnyxPins pins) : IOnyxPinsSource
{
    /// <inheritdoc />
    public OnyxPins Pins { get; set; } = pins;
}
