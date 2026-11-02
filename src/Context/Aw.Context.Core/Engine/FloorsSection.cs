using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.index.floors' as written: the dense leg's relevance floor (spec §6).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record FloorsSection(double? KeepDistance = null, double? RescueDistance = null, int? RescueHits = null);
