using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Surfaces;

/// <summary>Turns Jev's verdicts into override rows (spec §6.9, apply step).</summary>
public static class SurfaceApply
{
    /// <summary>Writes the override rows a verdict asks for.</summary>
    /// <param name="groups">The groups that were asked.</param>
    /// <param name="verdicts">The verdicts.</param>
    /// <param name="overrides">Every override of the map.</param>
    /// <param name="at">When the rows are written.</param>
    /// <returns>The new rows.</returns>
    public static IReadOnlyList<MapOverride> Rows(IReadOnlyList<SurfaceGroup> groups, IReadOnlyList<SurfaceVerdict> verdicts, IReadOnlyList<MapOverride> overrides, DateTimeOffset at) => throw new NotSupportedException();
}
