using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.checks.overCap': the shares of features over the file cap that trip it.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ChecksOverCapSection(int? DegradedPercent = null, int? RefusePercent = null);
