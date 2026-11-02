using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.checks.isolated': which features are measured for isolation and the shares that trip it.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ChecksIsolatedSection(int? MinFiles = null, int? MinFeatures = null, int? DegradedPercent = null, int? RefusePercent = null);
