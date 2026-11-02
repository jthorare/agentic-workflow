namespace Aw.Context.Core.Surfaces;

/// <summary>The verdict on one group (spec §6.9): keep separate, merge into a named survivor, or reject.</summary>
/// <param name="GroupKey">The <see cref="SurfaceGroup.Key"/> of the group it answers.</param>
/// <param name="Choice">What was decided.</param>
/// <param name="Survivor">For <see cref="SurfaceChoice.Merge"/>, the index in <see cref="SurfaceGroup.Candidates"/> of the candidate whose node keeps its key; otherwise null.</param>
/// <param name="SurvivorKey">For a merge, that candidate's node key (so a group rebuilt between ask and apply can't point at the wrong node); otherwise null.</param>
/// <param name="MergedName">For a merge, the survivor's name, cleaned like a label; otherwise null.</param>
/// <param name="Decided">False when the group is <see cref="SurfaceChoice.Separate"/> only because Jev was not asked, did not answer, answered off the options or was under the confidence bar. Store a decision only when true.</param>
public sealed record SurfaceVerdict(string GroupKey, SurfaceChoice Choice, int? Survivor, string? SurvivorKey, string? MergedName, bool Decided)
{
    /// <summary>Keep the group's candidates as they are.</summary>
    /// <param name="groupKey">The group's key.</param>
    /// <param name="decided">True when Jev chose it, false when it is the safe answer to doubt.</param>
    /// <returns>The verdict.</returns>
    public static SurfaceVerdict Separate(string groupKey, bool decided) => new(groupKey, SurfaceChoice.Separate, Survivor: null, SurvivorKey: null, MergedName: null, decided);

    /// <summary>Merge the group's candidates into the survivor.</summary>
    /// <param name="groupKey">The group's key.</param>
    /// <param name="survivor">The survivor's index in the group.</param>
    /// <param name="survivorKey">The survivor's node key.</param>
    /// <param name="name">The survivor's cleaned name.</param>
    /// <returns>The verdict.</returns>
    public static SurfaceVerdict Merged(string groupKey, int survivor, string survivorKey, string name) => new(groupKey, SurfaceChoice.Merge, survivor, survivorKey, name, Decided: true);

    /// <summary>Reject the group's candidates.</summary>
    /// <param name="groupKey">The group's key.</param>
    /// <returns>The verdict.</returns>
    public static SurfaceVerdict Rejected(string groupKey) => new(groupKey, SurfaceChoice.Reject, Survivor: null, SurvivorKey: null, MergedName: null, Decided: true);
}
