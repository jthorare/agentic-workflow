using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.ranker' as written: every key optional, unknown keys refused.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record RankerSection(bool? Features = null, RankerWeightsSection? Weights = null, bool? Rerank = null, int? RerankTop = null);
