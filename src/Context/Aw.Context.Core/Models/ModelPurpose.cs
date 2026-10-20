using System.Text.Json.Serialization;

namespace Aw.Context.Core.Models;

/// <summary>Why the engine asks a model; each purpose has its own profile switch and token budget (spec §3, §9.4).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ModelPurpose>))]
public enum ModelPurpose
{
    /// <summary>Map labeling (context.labeling).</summary>
    [JsonStringEnumMemberName("labeling")]
    Labeling,

    /// <summary>Decision extraction from records (context.labeling, maxTokensPerSync).</summary>
    [JsonStringEnumMemberName("decision-extraction")]
    DecisionExtraction,

    /// <summary>The rerank arm (Phase 8, off by default).</summary>
    [JsonStringEnumMemberName("rerank")]
    Rerank,

    /// <summary>The eval adjudicator (context.eval.adjudicator).</summary>
    [JsonStringEnumMemberName("adjudication")]
    Adjudication,

    /// <summary>The map cleanup agent: keep or remove each low-confidence proposal before an automatic promote.</summary>
    [JsonStringEnumMemberName("map-cleanup")]
    Cleanup,
}
