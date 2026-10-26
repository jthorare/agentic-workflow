using System.Text.Json.Serialization;
using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Results;

/// <summary>A query's answer (spec §8.4). Nothing is cut: conflicts, degraded sources and every ranked item are always whole.</summary>
/// <param name="SchemaVersion">The result format version (<see cref="CurrentSchemaVersion"/>).</param>
/// <param name="QueryId">The query's id in the query log.</param>
/// <param name="Query">The question as asked.</param>
/// <param name="Anchors">The anchors it resolved to (none for Phase 1's text shape).</param>
/// <param name="MapVersion">The map version it read, or null before a map exists.</param>
/// <param name="AsOf">When the answer was assembled.</param>
/// <param name="Sections">The sections in fill order: graph, affects, decisions, code, records.</param>
/// <param name="Conflicts">Typed disagreements between intent and behavior.</param>
/// <param name="SuggestedAnchors">Anchors the caller may use next.</param>
/// <param name="Degraded">Sources that didn't answer, each with its code.</param>
/// <param name="Cites">The citations, by marker ('C1', 'C2', …).</param>
/// <param name="Diff">The diff shape's facts, or null for every other shape (left out of JSON).</param>
public sealed record ContextResult(
    int SchemaVersion,
    QueryId QueryId,
    ContextQuery Query,
    IReadOnlyList<string> Anchors,
    string? MapVersion,
    DateTimeOffset AsOf,
    IReadOnlyList<ResultSection> Sections,
    IReadOnlyList<ResultConflict> Conflicts,
    IReadOnlyList<string> SuggestedAnchors,
    IReadOnlyList<DegradedSource> Degraded,
    IReadOnlyDictionary<string, Citation> Cites,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DiffPart? Diff = null)
{
    /// <summary>The result format this build writes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The code units the component hop skipped for the fan-out cap; left out when none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<SkippedComponent>? SkippedComponents { get; init; }

    /// <summary>How many demoted files the answer carries, per role ('index.demote'); left out when none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FilteredCounts? Filtered { get; init; }
}
