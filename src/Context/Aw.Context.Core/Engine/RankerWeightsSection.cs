using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.ranker.weights' as written.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record RankerWeightsSection(double? CascadeDistance = null, double? EdgeType = null, double? Provenance = null, double? Supersession = null, double? Recency = null);
