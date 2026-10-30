namespace Aw.Context.Core.Surfaces;

/// <summary>The verdict on one group (spec §6.9): keep separate, merge under a chosen name, or reject.</summary>
/// <param name="GroupKey">The <see cref="SurfaceGroup.Key"/> of the group it answers.</param>
/// <param name="Choice">What was decided.</param>
/// <param name="MergedName">For <see cref="SurfaceChoice.Merge"/>, exactly one candidate's name, cleaned like a label; otherwise null.</param>
/// <param name="Decided">False when the group is <see cref="SurfaceChoice.Separate"/> only because Jev was not asked, did not answer, answered off the options or was under the confidence bar.</param>
public sealed record SurfaceVerdict(string GroupKey, SurfaceChoice Choice, string? MergedName, bool Decided)
{
    /// <summary>Keep the group's candidates as they are.</summary>
    /// <param name="groupKey">The group's key.</param>
    /// <param name="decided">True when Jev chose it, false when it is the safe answer to doubt.</param>
    /// <returns>The verdict.</returns>
    public static SurfaceVerdict Separate(string groupKey, bool decided) => new(groupKey, SurfaceChoice.Separate, MergedName: null, decided);

    /// <summary>Merge the group's candidates into one node.</summary>
    /// <param name="groupKey">The group's key.</param>
    /// <param name="name">The chosen candidate's name.</param>
    /// <returns>The verdict.</returns>
    public static SurfaceVerdict Merged(string groupKey, string name) => new(groupKey, SurfaceChoice.Merge, name, Decided: true);

    /// <summary>Reject the group's candidates.</summary>
    /// <param name="groupKey">The group's key.</param>
    /// <returns>The verdict.</returns>
    public static SurfaceVerdict Rejected(string groupKey) => new(groupKey, SurfaceChoice.Reject, MergedName: null, Decided: true);
}
