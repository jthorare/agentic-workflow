using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.conceptScan.models.&lt;cli&gt;': the model name for each tier.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ConceptScanCliSection(string? Cheap = null, string? Strong = null);
