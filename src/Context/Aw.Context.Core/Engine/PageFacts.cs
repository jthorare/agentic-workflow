namespace Aw.Context.Core.Engine;

/// <summary>What the current map says, for <see cref="PageRules.Check"/>: the ids a page may point at and the repos each key is evidenced in.</summary>
/// <param name="ConceptIds">Ids of every concept in the map (pins and derived), without the 'concept:' prefix.</param>
/// <param name="SurfaceRepos">Each surface key in the map to the repos its 'implements' evidence is in.</param>
/// <param name="ActionRepos">Each action key in the map to the repos its evidence is in.</param>
public sealed record PageFacts(IReadOnlySet<ConceptId> ConceptIds, IReadOnlyDictionary<string, IReadOnlyList<string>> SurfaceRepos, IReadOnlyDictionary<string, IReadOnlyList<string>> ActionRepos)
{
    /// <summary>The stored form of a key a person wrote (the map's builder passes EntryKey.Normalized, issue 1790); null looks keys up only as written. TODO(P1d-2): the page write (ui P1b-2 writer) must pass it, with a test on that caller.</summary>
    public Func<string, string>? Stored { get; init; }
}
