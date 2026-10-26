using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.conceptScan' as written: the tier thresholds and the per-CLI model names.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ConceptScanSection(int? StrongAboveFeatures = null, int? StrongAbovePromptTokens = null, ConceptScanModelsSection? Models = null);
