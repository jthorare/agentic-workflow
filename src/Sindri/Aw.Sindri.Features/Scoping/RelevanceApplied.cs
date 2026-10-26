using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

/// <summary>What 'step.scope.relevance-apply' writes: the filtered lenses and coverage, the relevance record and the map fixes.</summary>
/// <param name="Lenses">The lenses fact without the entries that left.</param>
/// <param name="Coverage">The coverage fact rewritten from those lenses.</param>
/// <param name="Relevance">The relevance fact.</param>
/// <param name="MapFixes">The map fixes fact.</param>
internal sealed record RelevanceApplied(JsonObject Lenses, JsonObject Coverage, JsonObject Relevance, JsonObject MapFixes);
