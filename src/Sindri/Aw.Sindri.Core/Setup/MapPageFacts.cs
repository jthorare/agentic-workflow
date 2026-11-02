namespace Aw.Sindri.Core.Setup;

/// <summary>What the current map says, for a page write (pages spec §3.1): its concepts, and the repos each surface and action key is evidenced in. Plain strings, since Core can't see the context engine's types.</summary>
/// <param name="Version">The map read ('v41'), or null when no map is built yet (everything else is empty then).</param>
/// <param name="Concepts">Every concept of the map, derived and pinned.</param>
/// <param name="SurfaceRepos">Each surface key to the repos of its evidence.</param>
/// <param name="ActionRepos">Each action key to the repos of its evidence.</param>
public sealed record MapPageFacts(string? Version, IReadOnlyList<MapConcept> Concepts, IReadOnlyDictionary<string, IReadOnlyList<string>> SurfaceRepos, IReadOnlyDictionary<string, IReadOnlyList<string>> ActionRepos);
