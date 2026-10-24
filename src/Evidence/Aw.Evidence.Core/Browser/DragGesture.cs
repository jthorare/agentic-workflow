using Aw.Evidence.Core.Geometry;

namespace Aw.Evidence.Core.Browser;

/// <summary>Where a drag takes hold and lets go, and whether Alt is held through the release.</summary>
/// <param name="Grab">Pixels from the source's top-left corner where the press lands; null is its center.</param>
/// <param name="Drop">Pixels from the destination's top-left corner where the release lands; null is its center.</param>
/// <param name="HoldAlt">Whether Alt is held through the release.</param>
public sealed record DragGesture(Offset? Grab, Offset? Drop, bool HoldAlt);
