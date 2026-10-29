using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.conceptSynthesis' as written: the grouper's knobs, the prompt's character caps and the cover share.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ConceptSynthesisSection(
    bool? Enabled = null,
    int? Neighbours = null,
    double? MinCosine = null,
    int? MaxGroup = null,
    int? MinRecords = null,
    int? MinSources = null,
    int? MaxCharsPerRecord = null,
    int? MaxCharsPerCall = null,
    double? CoverShare = null);
