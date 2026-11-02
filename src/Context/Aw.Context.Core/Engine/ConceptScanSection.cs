using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.conceptScan' as written: the tier thresholds. 'models' is read only to refuse it with the key that replaced it (issue 1867).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ConceptScanSection(int? StrongAboveFeatures = null, int? StrongAbovePromptTokens = null, JsonElement? Models = null);
