namespace Aw.Context.Core.Surfaces;

/// <summary>A stored <see cref="SurfaceVerdict"/>: only decided verdicts are kept, with the group's node keys (never names or paths) and the model that decided.</summary>
/// <param name="GroupKey">The <see cref="SurfaceGroup.Key"/> it answers.</param>
/// <param name="Choice">What was decided.</param>
/// <param name="SurvivorKey">For a merge, the survivor's node key; otherwise null.</param>
/// <param name="MergedName">For a merge, the survivor's cleaned name; otherwise null.</param>
/// <param name="CandidateKeys">The node keys of the group's candidates when it was decided.</param>
/// <param name="Model">The identity of the model that decided, or null when unknown.</param>
/// <param name="DecidedAt">When it was decided.</param>
public sealed record SurfaceDecision(string GroupKey, SurfaceChoice Choice, string? SurvivorKey, string? MergedName, IReadOnlyList<string> CandidateKeys, string? Model, DateTimeOffset DecidedAt)
{
    /// <summary>Turns the stored decision back into a verdict for the group as it is now.</summary>
    /// <param name="group">The group as built this time.</param>
    /// <returns>A merge naming the survivor's index in the group, a reject, or separate (also when a merge's survivor is no longer in the group). Always decided.</returns>
    public SurfaceVerdict ToVerdict(SurfaceGroup group) => throw new NotSupportedException();

    /// <summary>Makes the decision to store from a decided verdict.</summary>
    /// <param name="verdict">The verdict.</param>
    /// <param name="group">The group it answers.</param>
    /// <param name="model">The deciding model's identity, or null.</param>
    /// <param name="at">When it was decided.</param>
    /// <returns>The decision.</returns>
    public static SurfaceDecision From(SurfaceVerdict verdict, SurfaceGroup group, string? model, DateTimeOffset at) => throw new NotSupportedException();
}
