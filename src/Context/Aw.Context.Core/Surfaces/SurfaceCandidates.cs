using Aw.Context.Core.Extract;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Surfaces;

/// <summary>Finds the action candidates of a build and groups the lookalikes (spec §6.9, apply step).</summary>
public static class SurfaceCandidates
{
    /// <summary>Groups the build's deterministic actions that look alike.</summary>
    /// <param name="found">The map this build found.</param>
    /// <param name="overrides">Every override of the map.</param>
    /// <returns>The groups.</returns>
    public static IReadOnlyList<SurfaceGroup> Groups(ExtractedMap found, IReadOnlyList<MapOverride> overrides) => throw new NotSupportedException();
}
