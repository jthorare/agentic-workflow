using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Ranking;

/// <summary>A fusion leg that carries keys only, in rank order (concept pins spec §3.3): no hits, so it adds a score and a 'why' to items already fused, never a new item.</summary>
/// <param name="Source">The leg's name in 'why' ('pin').</param>
/// <param name="Keys">The canonical keys, best first.</param>
public sealed record RankedLeg(SourceId Source, IReadOnlyList<RecordKey> Keys);
