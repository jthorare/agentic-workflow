using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Pages;

/// <summary>What the current map says about pages, for the page write (pages spec §3.1): the facts <see cref="PageRules.Check"/> needs and the concepts a name can resolve to.</summary>
/// <param name="MapVersion">The map's version; null when there is no map yet.</param>
/// <param name="Facts">The concept ids and the repos of each surface and action key (empty when there is no map).</param>
/// <param name="Concepts">Every concept in the map with its name.</param>
public sealed record PageMapFacts(string? MapVersion, PageFacts Facts, IReadOnlyList<PageMapConcept> Concepts);
