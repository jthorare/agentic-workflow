namespace Aw.Context.Core.Surfaces;

/// <summary>Jev's decision on candidate groups for the map's product layer (model routing spec §6.9). The context lane's build makes the groups and applies the verdicts; this port only decides.</summary>
public interface ISurfacePicker
{
    /// <summary>Asks Jev about each group (packed, split and stopped as the label pick is: spec §6.3 to §6.5) and answers every group, asked or not.</summary>
    /// <param name="groups">The groups, each 1 to <see cref="SurfaceGroup.MaxCandidates"/> candidates.</param>
    /// <param name="options">The pick setting, scrub patterns and token budget of this build.</param>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>A verdict per group in input order. Anything uncertain, unasked or unavailable is <see cref="SurfaceChoice.Separate"/>; it never fails.</returns>
    public ValueTask<SurfacePickResult> Decide(IReadOnlyList<SurfaceGroup> groups, SurfacePickOptions options, CancellationToken cancellationToken);
}
