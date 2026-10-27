using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.conceptScan.models': one entry per agent CLI the model port can run.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ConceptScanModelsSection(ConceptScanCliSection? Claude = null, ConceptScanCliSection? Codex = null, ConceptScanCliSection? Cursor = null);
