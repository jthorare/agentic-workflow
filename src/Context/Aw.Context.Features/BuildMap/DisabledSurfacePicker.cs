using Aw.Context.Core.Surfaces;

namespace Aw.Context.Features.BuildMap;

/// <summary>The picker a host without a model gets: nothing is asked, every group stays separate and undecided, so nothing is stored.</summary>
internal sealed class DisabledSurfacePicker : ISurfacePicker
{
    public ValueTask<SurfacePickResult> Decide(IReadOnlyList<SurfaceGroup> groups, SurfacePickOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(groups);
        return ValueTask.FromResult(new SurfacePickResult([.. groups.Select(static g => SurfaceVerdict.Separate(g.Key, decided: false))], new SurfaceCounts(0, groups.Count, 0, groups.Count), "off"));
    }
}
