using Aw.Context.Core.Docs;
using Aw.Context.Core.Map;
using Aw.Context.Core.Ranking;

namespace Aw.Context.Features.QueryContext;

/// <summary>What the pipeline answers from: the map it reads, the sources it searches and the time the sources are cut to (null: now).</summary>
/// <param name="Map">The map provider.</param>
/// <param name="Catalog">The source catalog.</param>
/// <param name="AsOf">The time every source is asked to cut to, or null for the live query.</param>
/// <param name="Diff">The diff shape's anchoring (the live query only; a backtest arm never asks a diff).</param>
/// <param name="Ranker">The ranker stages a backtest arm forces, or null for the profile's (the live query).</param>
/// <param name="Concept">The pin the query is scoped to, or null; the request's own field takes over from it (so the phases that read the pin are tested through it first).</param>
/// <param name="Rebuild">Where the map's rebuild stands, for the stale-map note; null when the view asks for none (a backtest arm).</param>
internal sealed record PipelineInputs(IMapProvider Map, ISourceCatalog Catalog, DateTimeOffset? AsOf = null, DiffAnswer? Diff = null, RankerMode? Ranker = null, string? Concept = null, IMapRebuildState? Rebuild = null);
